namespace DockerContainerUpdateChecker.Models;

public sealed record ContainerUpdateInfo(
    string ContainerName,
    string ImageName,
    string CurrentDigest,
    string RemoteDigest);
