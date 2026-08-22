using DockerContainerUpdateChecker.Models;
using System.Globalization;
using System.Net;

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
        var startedAt = timeProvider.GetTimestamp();
        var updates = new List<ContainerUpdateInfo>();
        var errors = new List<string>();
        var outcomes = new List<ContainerCheckOutcome>();
        logger.LogInformation("Starting container update check at local time {CheckedAtLocal}.", checkedAtLocalText);
        var containers = await containerCatalog.GetRunningContainersAsync(cancellationToken);
        logger.LogInformation("Loaded {ContainerCount} running container(s) for update evaluation.", containers.Count);

        foreach (var container in containers)
        {
            if (containerCatalog.IsExcluded(container.Name, container.ContainerId))
            {
                outcomes.Add(new ContainerCheckOutcome(
                    container.Name,
                    container.ImageName,
                    ContainerCheckStatus.Skipped,
                    ContainerCheckReason.Excluded));
                logger.LogInformation(
                    "Skipping container {ContainerName} because it is excluded by configuration.",
                    container.Name);
                continue;
            }

            if (container.ImageReference.IsDigestPinned)
            {
                outcomes.Add(new ContainerCheckOutcome(
                    container.Name,
                    container.ImageName,
                    ContainerCheckStatus.NotCheckable,
                    ContainerCheckReason.DigestPinned));
                logger.LogInformation(
                    "Container {ContainerName} is not checkable because image {ImageName} is digest-pinned.",
                    container.Name,
                    container.ImageName);
                continue;
            }

            if (container.ImageReference.Registry.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                outcomes.Add(new ContainerCheckOutcome(
                    container.Name,
                    container.ImageName,
                    ContainerCheckStatus.NotCheckable,
                    ContainerCheckReason.LocalImage));
                logger.LogInformation(
                    "Container {ContainerName} is not checkable because image {ImageName} points to a local registry reference.",
                    container.Name,
                    container.ImageName);
                continue;
            }

            if (string.IsNullOrWhiteSpace(container.LocalDigest))
            {
                outcomes.Add(new ContainerCheckOutcome(
                    container.Name,
                    container.ImageName,
                    ContainerCheckStatus.NotCheckable,
                    ContainerCheckReason.MissingLocalDigest));
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
                    outcomes.Add(new ContainerCheckOutcome(
                        container.Name,
                        container.ImageName,
                        ContainerCheckStatus.UpdateAvailable,
                        ContainerCheckReason.None));
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
                    outcomes.Add(new ContainerCheckOutcome(
                        container.Name,
                        container.ImageName,
                        ContainerCheckStatus.UpToDate,
                        ContainerCheckReason.None));
                    logger.LogInformation(
                        "Container {ContainerName} is up to date for image {ImageName}.",
                        container.Name,
                        container.ImageName);
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                outcomes.Add(new ContainerCheckOutcome(
                    container.Name,
                    container.ImageName,
                    ContainerCheckStatus.NotCheckable,
                    ContainerCheckReason.RegistryAuthFailed,
                    ex.Message));
                logger.LogWarning(
                    ex,
                    "Container {ContainerName} is not checkable because registry authentication failed.",
                    container.Name);
                errors.Add($"Container '{container.Name}': registry authentication failed.");
            }
            catch (HttpRequestException ex) when (ex.StatusCode is null || ex.StatusCode == HttpStatusCode.BadGateway || ex.StatusCode == HttpStatusCode.ServiceUnavailable || ex.StatusCode == HttpStatusCode.GatewayTimeout)
            {
                outcomes.Add(new ContainerCheckOutcome(
                    container.Name,
                    container.ImageName,
                    ContainerCheckStatus.NotCheckable,
                    ContainerCheckReason.RegistryUnavailable,
                    ex.Message));
                logger.LogWarning(
                    ex,
                    "Container {ContainerName} is not checkable because the registry is unavailable.",
                    container.Name);
                errors.Add($"Container '{container.Name}': registry unavailable.");
            }
            catch (Exception ex)
            {
                outcomes.Add(new ContainerCheckOutcome(
                    container.Name,
                    container.ImageName,
                    ContainerCheckStatus.Error,
                    ContainerCheckReason.None,
                    ex.Message));
                logger.LogWarning(ex, "Failed to check updates for container {ContainerName}", container.Name);
                errors.Add($"Container '{container.Name}': {ex.Message}");
            }
        }

        logger.LogInformation(
            "Finished update check at local time {CheckedAtLocal}. Scanned: {ScannedCount}. Updates: {UpdateCount}. Up to date: {UpToDateCount}. Skipped: {SkippedCount}. Not checkable: {NotCheckableCount}. Errors: {ErrorCount}.",
            checkedAtLocalText,
            containers.Count,
            outcomes.Count(outcome => outcome.Status == ContainerCheckStatus.UpdateAvailable),
            outcomes.Count(outcome => outcome.Status == ContainerCheckStatus.UpToDate),
            outcomes.Count(outcome => outcome.Status == ContainerCheckStatus.Skipped),
            outcomes.Count(outcome => outcome.Status == ContainerCheckStatus.NotCheckable),
            outcomes.Count(outcome => outcome.Status == ContainerCheckStatus.Error));
        return new UpdateCheckResult(
            checkedAtUtc,
            timeProvider.GetElapsedTime(startedAt),
            containers.Count,
            outcomes,
            updates,
            errors);
    }
}
