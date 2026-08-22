namespace DockerContainerUpdateChecker.Services;

public sealed record TelegramBotIdentity(
    long Id,
    string FirstName,
    string? Username);
