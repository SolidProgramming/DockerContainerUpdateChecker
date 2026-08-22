using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DockerContainerUpdateChecker.Configuration;
using Microsoft.Extensions.Options;

namespace DockerContainerUpdateChecker.Services;

public sealed class TelegramNotifier(
    IHttpClientFactory httpClientFactory,
    IOptions<TelegramOptions> telegramOptions) : ITelegramNotifier
{
    public const string HttpClientName = "telegram";

    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    private readonly TelegramOptions telegramOptions = telegramOptions.Value;

    public async Task SendAsync(string message, CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient(HttpClientName);
        var requestUri = $"https://api.telegram.org/bot{telegramOptions.BotToken}/sendMessage";

        using var response = await client.PostAsJsonAsync(
            requestUri,
            new TelegramSendMessageRequest(telegramOptions.ChatId, message),
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private sealed record TelegramSendMessageRequest(
        [property: JsonPropertyName("chat_id")] string ChatId,
        [property: JsonPropertyName("text")] string Text);
}
