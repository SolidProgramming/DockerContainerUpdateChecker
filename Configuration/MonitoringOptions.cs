namespace DockerContainerUpdateChecker.Configuration;

public sealed class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    public string[] ExcludedContainers { get; set; } = [];
}
