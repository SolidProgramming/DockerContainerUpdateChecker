using DockerContainerUpdateChecker.Models;

namespace DockerContainerUpdateChecker.Services;

public sealed class ContainerUpdateChecker(
    IContainerCatalog containerCatalog,
    IRegistryManifestClient registryManifestClient,
    ILogger<ContainerUpdateChecker> logger,
    TimeProvider timeProvider) : IContainerUpdateChecker
{
    private readonly IContainerCatalog containerCatalog = containerCatalog;
    private readonly IRegistryManifestClient registryManifestClient = registryManifestClient;
    private readonly ILogger<ContainerUpdateChecker> logger = logger;
    private readonly TimeProvider timeProvider = timeProvider;

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        var checkedAtUtc = timeProvider.GetUtcNow();
        var updates = new List<ContainerUpdateInfo>();
        var errors = new List<string>();
        var containers = await containerCatalog.GetRunningContainersAsync(cancellationToken);

        foreach (var container in containers)
        {
            if (string.IsNullOrWhiteSpace(container.LocalDigest))
            {
                errors.Add($"Container '{container.Name}' has no local repo digest; skipping update check.");
                continue;
            }

            try
            {
                var remoteDigest = await registryManifestClient.GetRemoteDigestAsync(container.ImageReference, cancellationToken);
                if (!string.Equals(container.LocalDigest, remoteDigest, StringComparison.OrdinalIgnoreCase))
                {
                    updates.Add(new ContainerUpdateInfo(
                        container.Name,
                        container.ImageName,
                        container.LocalDigest,
                        remoteDigest));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to check updates for container {ContainerName}", container.Name);
                errors.Add($"Container '{container.Name}': {ex.Message}");
            }
        }

        return new UpdateCheckResult(checkedAtUtc, updates, errors);
    }
}
