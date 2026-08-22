using DockerContainerUpdateChecker.Models;

namespace DockerContainerUpdateChecker.Services;

public interface IUpdateMessageFormatter
{
    string Format(PersistedUpdateState? previous, UpdateCheckResult current);
}
