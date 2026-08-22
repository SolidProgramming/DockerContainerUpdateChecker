using DockerContainerUpdateChecker.Models;
using System.Globalization;

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
        var checkedAtLocal = TimeZoneInfo.ConvertTime(checkedAtUtc, timeProvider.LocalTimeZone);
        var checkedAtLocalText = checkedAtLocal.ToString("G", CultureInfo.CurrentCulture);
        var updates = new List<ContainerUpdateInfo>();
        var errors = new List<string>();
        logger.LogInformation("Starting container update check at local time {CheckedAtLocal}.", checkedAtLocalText);
        var containers = await containerCatalog.GetRunningContainersAsync(cancellationToken);
        logger.LogInformation("Loaded {ContainerCount} running container(s) for update evaluation.", containers.Count);

        foreach (var container in containers)
        {
            if (string.IsNullOrWhiteSpace(container.LocalDigest))
            {
                logger.LogWarning(
                    "Skipping container {ContainerName} because no local repo digest is available for image {ImageName}.",
                    container.Name,
                    container.ImageName);
                errors.Add($"Container '{container.Name}' has no local repo digest; skipping update check.");
                continue;
            }

            try
            {
                var remoteDigest = await registryManifestClient.GetRemoteDigestAsync(container.ImageReference, cancellationToken);
                if (!string.Equals(container.LocalDigest, remoteDigest, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation(
                        "Update detected for container {ContainerName}: local digest {LocalDigest}, remote digest {RemoteDigest}.",
                        container.Name,
                        container.LocalDigest,
                        remoteDigest);
                    updates.Add(new ContainerUpdateInfo(
                        container.Name,
                        container.ImageName,
                        container.LocalDigest,
                        remoteDigest));
                }
                else
                {
                    logger.LogInformation(
                        "Container {ContainerName} is up to date for image {ImageName}.",
                        container.Name,
                        container.ImageName);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to check updates for container {ContainerName}", container.Name);
                errors.Add($"Container '{container.Name}': {ex.Message}");
            }
        }

        logger.LogInformation(
            "Finished update check at local time {CheckedAtLocal}. Updates found: {UpdateCount}. Errors: {ErrorCount}.",
            checkedAtLocalText,
            updates.Count,
            errors.Count);
        return new UpdateCheckResult(checkedAtUtc, updates, errors);
    }
}
