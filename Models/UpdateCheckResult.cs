namespace DockerContainerUpdateChecker.Models;

public sealed record UpdateCheckResult(
    DateTimeOffset CheckedAtUtc,
    TimeSpan Duration,
    int CheckedContainerCount,
    IReadOnlyList<ContainerCheckOutcome> Outcomes,
    IReadOnlyList<ContainerUpdateInfo> Updates,
    IReadOnlyList<string> Errors)
{
    public int UpdateCount => Outcomes.Count(outcome => outcome.Status == ContainerCheckStatus.UpdateAvailable);

    public int UpToDateCount => Outcomes.Count(outcome => outcome.Status == ContainerCheckStatus.UpToDate);

    public int SkippedCount => Outcomes.Count(outcome => outcome.Status == ContainerCheckStatus.Skipped);

    public int NotCheckableCount => Outcomes.Count(outcome => outcome.Status == ContainerCheckStatus.NotCheckable);

    public int ErrorCount => Outcomes.Count(outcome => outcome.Status == ContainerCheckStatus.Error);
}
