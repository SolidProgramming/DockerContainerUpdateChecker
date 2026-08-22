namespace DockerContainerUpdateChecker.Configuration;

public sealed class DockerOptions
{
    public const string SectionName = "Docker";

    public string? Endpoint { get; set; }
}
