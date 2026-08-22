namespace DockerContainerUpdateChecker.Services;

public static class DockerEndpointResolver
{
    public static Uri Resolve(string? configuredEndpoint)
    {
        if (!string.IsNullOrWhiteSpace(configuredEndpoint))
        {
            return new Uri(configuredEndpoint);
        }

        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (!string.IsNullOrWhiteSpace(dockerHost))
        {
            return new Uri(dockerHost);
        }

        return OperatingSystem.IsWindows()
            ? new Uri("npipe://./pipe/docker_engine")
            : new Uri("unix:///var/run/docker.sock");
    }
}
