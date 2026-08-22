namespace DockerContainerUpdateChecker.Models;

public sealed record TelegramMessage(
    string Text,
    string? ParseMode = null);
