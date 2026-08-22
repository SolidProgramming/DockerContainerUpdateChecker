using DockerContainerUpdateChecker.Services;

namespace DockerContainerUpdateChecker.Jobs;

public sealed class TelegramTestJob(
    ITelegramNotifier telegramNotifier,
    TimeProvider timeProvider,
    ILogger<TelegramTestJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var localNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeProvider.LocalTimeZone);
        logger.LogWarning("Telegram test job started at local time {LocalNow}.", localNow);

        var message = $"Telegram test notification sent at {localNow:yyyy-MM-dd HH:mm:ss zzz} local time.";
        await telegramNotifier.SendAsync(message, cancellationToken);

        logger.LogWarning("Telegram test job finished successfully.");
    }
}
