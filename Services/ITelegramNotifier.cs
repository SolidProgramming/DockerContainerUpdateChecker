using DockerContainerUpdateChecker.Models;

namespace DockerContainerUpdateChecker.Services;

public interface ITelegramNotifier
{
    Task<TelegramBotIdentity> GetBotIdentityAsync(CancellationToken cancellationToken);

    Task SendAsync(TelegramMessage message, CancellationToken cancellationToken, bool silent = false);
}
