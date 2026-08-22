using DockerContainerUpdateChecker.Models;

namespace DockerContainerUpdateChecker.Services;

public static class ImageReferenceParser
{
    public static bool TryParse(string rawValue, out ImageReference imageReference)
    {
        imageReference = default!;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var working = rawValue.Trim();
        var digestIndex = working.IndexOf('@');
        var isDigestPinned = digestIndex >= 0;
        var nameWithoutDigest = digestIndex >= 0 ? working[..digestIndex] : working;

        var lastSlashIndex = nameWithoutDigest.LastIndexOf('/');
        var lastColonIndex = nameWithoutDigest.LastIndexOf(':');
        var hasExplicitTag = lastColonIndex > lastSlashIndex;
        var tag = hasExplicitTag ? nameWithoutDigest[(lastColonIndex + 1)..] : "latest";
        var repositoryPart = hasExplicitTag ? nameWithoutDigest[..lastColonIndex] : nameWithoutDigest;

        var segments = repositoryPart.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        string registry;
        string repository;

        if (IsRegistrySegment(segments[0]))
        {
            registry = segments[0];
            repository = string.Join('/', segments.Skip(1));
        }
        else
        {
            registry = "docker.io";
            repository = segments.Length == 1
                ? $"library/{segments[0]}"
                : string.Join('/', segments);
        }

        if (string.IsNullOrWhiteSpace(repository))
        {
            return false;
        }

        imageReference = new ImageReference(
            Registry: registry,
            Repository: repository,
            Tag: tag,
            OriginalName: working,
            RegistryRepository: $"{registry}/{repository}",
            IsDigestPinned: isDigestPinned);

        return true;
    }

    private static bool IsRegistrySegment(string segment)
    {
        return segment.Contains('.', StringComparison.Ordinal)
            || segment.Contains(':', StringComparison.Ordinal)
            || segment.Equals("localhost", StringComparison.OrdinalIgnoreCase);
    }
}
