namespace DockerContainerUpdateChecker.Configuration;

public sealed class ServerOptions
{
    public const string SectionName = "Server";

    public int Port { get; set; } = 8080;
}
