using DockerContainerUpdateChecker.Models;

namespace DockerContainerUpdateChecker.Services;

public interface IUpdateStateComparer
{
    bool HasChanged(PersistedUpdateState? previous, UpdateCheckResult current);

    PersistedUpdateState CreateState(UpdateCheckResult current);
}
