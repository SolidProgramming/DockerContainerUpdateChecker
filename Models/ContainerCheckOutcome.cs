namespace DockerContainerUpdateChecker.Models;

public sealed record ContainerCheckOutcome(
    string ContainerName,
    string ImageName,
    ContainerCheckStatus Status,
    ContainerCheckReason Reason,
    string? Details = null);
