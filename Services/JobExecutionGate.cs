namespace DockerContainerUpdateChecker.Services;

public sealed class JobExecutionGate
{
    private readonly SemaphoreSlim semaphore = new(1, 1);

    public async Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken)
    {
        var acquired = await semaphore.WaitAsync(0, cancellationToken);
        return acquired ? new Releaser(semaphore) : null;
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
