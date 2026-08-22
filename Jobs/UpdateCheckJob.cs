using DockerContainerUpdateChecker.Services;

namespace DockerContainerUpdateChecker.Jobs;

public sealed class UpdateCheckJob(
    IContainerUpdateChecker updateChecker,
    IUpdateStateStore updateStateStore,
    IUpdateStateComparer updateStateComparer,
    IUpdateMessageFormatter updateMessageFormatter,
    ITelegramNotifier telegramNotifier,
    JobExecutionGate executionGate,
    ILogger<UpdateCheckJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var lease = await executionGate.TryAcquireAsync(cancellationToken);
        if (lease is null)
        {
            logger.LogInformation("Skipping update check because another run is already active.");
            return;
        }

        logger.LogWarning("Update check job started.");
        var previousState = await updateStateStore.LoadAsync(cancellationToken);
        var currentResult = await updateChecker.CheckAsync(cancellationToken);
        var currentState = updateStateComparer.CreateState(currentResult);

        if (updateStateComparer.HasChanged(previousState, currentResult))
        {
            logger.LogWarning(
                "Update state changed. Sending Telegram notification for {UpdateCount} update(s).",
                currentResult.Updates.Count);
            var message = updateMessageFormatter.Format(previousState, currentResult);
            await telegramNotifier.SendAsync(message, cancellationToken);
        }
        else
        {
            logger.LogWarning("Update state unchanged. No Telegram notification will be sent.");
        }

        await updateStateStore.SaveAsync(currentState, cancellationToken);
        logger.LogWarning("Update check job finished and state was persisted.");
    }
}
