using DockerContainerUpdateChecker.Models;

namespace DockerContainerUpdateChecker.Services;

public interface IRegistryManifestClient
{
    Task<string> GetRemoteDigestAsync(ImageReference imageReference, CancellationToken cancellationToken);
}
