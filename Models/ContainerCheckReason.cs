namespace DockerContainerUpdateChecker.Models;

public enum ContainerCheckReason
{
    None,
    Excluded,
    DigestPinned,
    LocalImage,
    MissingLocalDigest,
    RegistryAuthFailed,
    RegistryUnavailable
}
