using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using DockerContainerUpdateChecker.Models;

namespace DockerContainerUpdateChecker.Services;

public sealed partial class RegistryManifestClient(IHttpClientFactory httpClientFactory) : IRegistryManifestClient
{
    public const string HttpClientName = "registry";

    // Docker Hub can throttle manifest and token requests independently.  Keep the
    // retries here so every caller gets the same, registry-friendly behaviour.
    private const int MaxRateLimitAttempts = 4;
    private static readonly TimeSpan InitialRateLimitDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxRateLimitDelay = TimeSpan.FromMinutes(1);

    private static readonly string[] AcceptedManifestTypes =
    [
        "application/vnd.oci.image.index.v1+json",
        "application/vnd.docker.distribution.manifest.list.v2+json",
        "application/vnd.oci.image.manifest.v1+json",
        "application/vnd.docker.distribution.manifest.v2+json"
    ];

    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;

    public async Task<string> GetRemoteDigestAsync(ImageReference imageReference, CancellationToken cancellationToken)
    {
        if (imageReference.IsDigestPinned)
        {
            throw new InvalidOperationException($"Image '{imageReference.OriginalName}' is pinned by digest and will not move with a tag.");
        }

        using var client = httpClientFactory.CreateClient(HttpClientName);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await GetRemoteDigestOnceAsync(client, imageReference, cancellationToken);
            }
            catch (RegistryRateLimitException ex) when (attempt < MaxRateLimitAttempts)
            {
                var delay = ex.RetryAfter ?? GetExponentialDelay(attempt);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static async Task<string> GetRemoteDigestOnceAsync(
        HttpClient client,
        ImageReference imageReference,
        CancellationToken cancellationToken)
    {
        var manifestUri = BuildManifestUri(imageReference);

        using var initialRequest = CreateManifestRequest(manifestUri, bearerToken: null);
        using var initialResponse = await client.SendAsync(initialRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        ThrowIfRateLimited(initialResponse);
        if (initialResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            var bearerToken = await ResolveBearerTokenAsync(client, initialResponse, imageReference, cancellationToken);
            using var authorizedRequest = CreateManifestRequest(manifestUri, bearerToken);
            using var authorizedResponse = await client.SendAsync(authorizedRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            ThrowIfRateLimited(authorizedResponse);
            authorizedResponse.EnsureSuccessStatusCode();
            return await ExtractDigestAsync(authorizedResponse, cancellationToken);
        }

        initialResponse.EnsureSuccessStatusCode();
        return await ExtractDigestAsync(initialResponse, cancellationToken);
    }

    private static Uri BuildManifestUri(ImageReference imageReference)
    {
        var registryHost = imageReference.Registry.Equals("docker.io", StringComparison.OrdinalIgnoreCase)
            ? "registry-1.docker.io"
            : imageReference.Registry;

        return new Uri($"https://{registryHost}/v2/{imageReference.Repository}/manifests/{imageReference.Tag}");
    }

    private static HttpRequestMessage CreateManifestRequest(Uri manifestUri, string? bearerToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, manifestUri);

        foreach (var mediaType in AcceptedManifestTypes)
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));
        }

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return request;
    }

    private static async Task<string> ExtractDigestAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Headers.TryGetValues("Docker-Content-Digest", out var digestHeaders))
        {
            var digest = digestHeaders.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(digest))
            {
                return digest;
            }
        }

        var contentBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var hash = SHA256.HashData(contentBytes);
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static async Task<string> ResolveBearerTokenAsync(
        HttpClient client,
        HttpResponseMessage unauthorizedResponse,
        ImageReference imageReference,
        CancellationToken cancellationToken)
    {
        var challenge = unauthorizedResponse.Headers.WwwAuthenticate
            .FirstOrDefault(header => header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase));

        if (challenge is null || string.IsNullOrWhiteSpace(challenge.Parameter))
        {
            throw new InvalidOperationException(
                $"Registry '{imageReference.Registry}' requires authentication but did not return a Bearer challenge.");
        }

        var values = ParseChallengeParameters(challenge.Parameter);

        if (!values.TryGetValue("realm", out var realm) || string.IsNullOrWhiteSpace(realm))
        {
            throw new InvalidOperationException($"Registry '{imageReference.Registry}' returned an invalid Bearer challenge.");
        }

        var queryParameters = new List<string>();

        if (values.TryGetValue("service", out var service) && !string.IsNullOrWhiteSpace(service))
        {
            queryParameters.Add($"service={Uri.EscapeDataString(service)}");
        }

        var scope = values.TryGetValue("scope", out var providedScope) && !string.IsNullOrWhiteSpace(providedScope)
            ? providedScope
            : $"repository:{imageReference.Repository}:pull";

        queryParameters.Add($"scope={Uri.EscapeDataString(scope)}");

        var tokenUri = $"{realm}?{string.Join('&', queryParameters)}";
        using var tokenResponse = await client.GetAsync(tokenUri, cancellationToken);
        ThrowIfRateLimited(tokenResponse);
        tokenResponse.EnsureSuccessStatusCode();

        await using var tokenStream = await tokenResponse.Content.ReadAsStreamAsync(cancellationToken);
        var tokenDocument = await JsonDocument.ParseAsync(tokenStream, cancellationToken: cancellationToken);

        if (tokenDocument.RootElement.TryGetProperty("token", out var tokenElement))
        {
            return tokenElement.GetString()
                ?? throw new InvalidOperationException("Registry token response did not contain a usable token.");
        }

        if (tokenDocument.RootElement.TryGetProperty("access_token", out var accessTokenElement))
        {
            return accessTokenElement.GetString()
                ?? throw new InvalidOperationException("Registry token response did not contain a usable access token.");
        }

        throw new InvalidOperationException("Registry token response did not contain a bearer token.");
    }

    private static void ThrowIfRateLimited(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.TooManyRequests)
        {
            return;
        }

        var retryAfter = response.Headers.RetryAfter?.Delta
            ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow);
        throw new RegistryRateLimitException(
            retryAfter.HasValue && retryAfter.Value > TimeSpan.Zero ? retryAfter : null);
    }

    private static TimeSpan GetExponentialDelay(int failedAttempt)
    {
        var delaySeconds = InitialRateLimitDelay.TotalSeconds * Math.Pow(2, failedAttempt - 1);
        return TimeSpan.FromSeconds(Math.Min(delaySeconds, MaxRateLimitDelay.TotalSeconds));
    }

    private static Dictionary<string, string> ParseChallengeParameters(string challenge)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in ChallengeValueRegex().Matches(challenge))
        {
            values[match.Groups["key"].Value] = match.Groups["value"].Value;
        }

        return values;
    }

    [GeneratedRegex("(?<key>[a-zA-Z]+)=\"(?<value>[^\"]*)\"")]
    private static partial Regex ChallengeValueRegex();

    private sealed class RegistryRateLimitException(TimeSpan? retryAfter)
        : HttpRequestException("The container registry temporarily rate-limited the request.", null, HttpStatusCode.TooManyRequests)
    {
        public TimeSpan? RetryAfter { get; } = retryAfter;
    }
}
