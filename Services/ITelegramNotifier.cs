namespace DockerContainerUpdateChecker.Services;

public interface ITelegramNotifier
{
    Task SendAsync(string message, CancellationToken cancellationToken);
}
