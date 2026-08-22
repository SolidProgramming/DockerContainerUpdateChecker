using DockerContainerUpdateChecker.Models;

namespace DockerContainerUpdateChecker.Services;

public interface IContainerUpdateChecker
{
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken);
}
