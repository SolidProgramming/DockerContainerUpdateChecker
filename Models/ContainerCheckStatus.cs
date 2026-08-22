namespace DockerContainerUpdateChecker.Models;

public enum ContainerCheckStatus
{
    UpdateAvailable,
    UpToDate,
    Skipped,
    NotCheckable,
    Error
}
