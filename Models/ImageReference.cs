namespace DockerContainerUpdateChecker.Models;

public sealed record ImageReference(
    string Registry,
    string Repository,
    string Tag,
    string OriginalName,
    string RegistryRepository,
    bool IsDigestPinned);
