using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DockerContainerUpdateChecker.Configuration;
using DockerContainerUpdateChecker.Models;
using Microsoft.Extensions.Options;

namespace DockerContainerUpdateChecker.Services;

public sealed class TelegramNotifier(
    IHttpClientFactory httpClientFactory,
    IOptions<TelegramOptions> telegramOptions,
    ILogger<TelegramNotifier> logger) : ITelegramNotifier
{
    public const string HttpClientName = "telegram";

    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    private readonly TelegramOptions telegramOptions = telegramOptions.Value;
    private readonly ILogger<TelegramNotifier> logger = logger;

    public async Task<TelegramBotIdentity> GetBotIdentityAsync(CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient(HttpClientName);
        var requestUri = $"https://api.telegram.org/bot{telegramOptions.BotToken}/getMe";

        using var response = await client.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<TelegramGetMeResponse>(stream, cancellationToken: cancellationToken);

        if (payload is null || !payload.Ok || payload.Result is null)
        {
            throw new InvalidOperationException("Telegram getMe did not return a valid bot identity.");
        }

        return new TelegramBotIdentity(
            payload.Result.Id,
            payload.Result.FirstName,
            payload.Result.Username);
    }

    public async Task SendAsync(TelegramMessage message, CancellationToken cancellationToken, bool silent = false)
    {
        using var client = httpClientFactory.CreateClient(HttpClientName);
        var requestUri = $"https://api.telegram.org/bot{telegramOptions.BotToken}/sendMessage";

        using var response = await client.PostAsJsonAsync(
            requestUri,
            new TelegramSendMessageRequest(
                telegramOptions.ChatId,
                message.Text,
                silent,
                message.ParseMode),
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            logger.LogWarning(
                "Telegram returned 400 Bad Request while sending a message to chat id {ChatId}. " +
                "A common reason is that the target user or group has not started a chat with the bot yet " +
                "for example by sending '/start', or the configured chat id is wrong.",
                telegramOptions.ChatId);
        }

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Telegram sendMessage failed with status {(int)response.StatusCode} ({response.ReasonPhrase}). Response: {responseBody}",
                null,
                response.StatusCode);
        }
    }

    private sealed record TelegramSendMessageRequest(
        [property: JsonPropertyName("chat_id")] string ChatId,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("disable_notification")] bool DisableNotification,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [property: JsonPropertyName("parse_mode")] string? ParseMode);

    private sealed record TelegramGetMeResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("result")] TelegramBotResult? Result);

    private sealed record TelegramBotResult(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("first_name")] string FirstName,
        [property: JsonPropertyName("username")] string? Username);
}
