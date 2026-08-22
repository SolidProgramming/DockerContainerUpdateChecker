namespace DockerContainerUpdateChecker.Models;

public sealed record ContainerDescriptor(
    string ContainerId,
    string Name,
    string ImageName,
    ImageReference ImageReference,
    string? LocalDigest);
