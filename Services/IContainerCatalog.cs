using DockerContainerUpdateChecker.Models;

namespace DockerContainerUpdateChecker.Services;

public interface IContainerCatalog
{
    Task<IReadOnlyList<ContainerDescriptor>> GetRunningContainersAsync(CancellationToken cancellationToken);
}
