using DockerContainerUpdateChecker.Models;

namespace DockerContainerUpdateChecker.Services;

public interface IUpdateStateStore
{
    Task<PersistedUpdateState?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(PersistedUpdateState state, CancellationToken cancellationToken);
}
