namespace DockerContainerUpdateChecker.Models;

public sealed record UpdateCheckResult(
    DateTimeOffset CheckedAtUtc,
    TimeSpan Duration,
    int CheckedContainerCount,
    IReadOnlyList<ContainerUpdateInfo> Updates,
    IReadOnlyList<string> Errors);
