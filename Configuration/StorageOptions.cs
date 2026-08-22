namespace DockerContainerUpdateChecker.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string DataDirectory { get; set; } = "/data";
}
