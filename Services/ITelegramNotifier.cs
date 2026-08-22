namespace DockerContainerUpdateChecker.Services;

public interface ITelegramNotifier
{
    Task<TelegramBotIdentity> GetBotIdentityAsync(CancellationToken cancellationToken);

    Task SendAsync(string message, CancellationToken cancellationToken);
}
