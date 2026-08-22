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

        var previousState = await updateStateStore.LoadAsync(cancellationToken);
        var currentResult = await updateChecker.CheckAsync(cancellationToken);
        var currentState = updateStateComparer.CreateState(currentResult);

        if (updateStateComparer.HasChanged(previousState, currentResult))
        {
            var message = updateMessageFormatter.Format(previousState, currentResult);
            await telegramNotifier.SendAsync(message, cancellationToken);
        }

        await updateStateStore.SaveAsync(currentState, cancellationToken);
    }
}
