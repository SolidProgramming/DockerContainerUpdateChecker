namespace DockerContainerUpdateChecker.Models;

public sealed class PersistedUpdateState
{
    public DateTimeOffset LastCheckedAtUtc { get; set; }

    public List<PersistedUpdateEntry> Updates { get; set; } = [];
}

public sealed class PersistedUpdateEntry
{
    public string ContainerName { get; set; } = string.Empty;

    public string ImageName { get; set; } = string.Empty;

    public string CurrentDigest { get; set; } = string.Empty;

    public string RemoteDigest { get; set; } = string.Empty;
}
