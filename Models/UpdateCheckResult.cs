namespace DockerContainerUpdateChecker.Models;

public sealed record UpdateCheckResult(
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<ContainerUpdateInfo> Updates,
    IReadOnlyList<string> Errors);
