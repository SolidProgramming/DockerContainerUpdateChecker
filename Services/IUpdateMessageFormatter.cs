using DockerContainerUpdateChecker.Models;

namespace DockerContainerUpdateChecker.Services;

public interface IUpdateMessageFormatter
{
    TelegramMessage Format(PersistedUpdateState? previous, UpdateCheckResult current);
}
