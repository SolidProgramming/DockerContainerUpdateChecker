using Docker.DotNet;
using Docker.DotNet.Models;
using DockerContainerUpdateChecker.Configuration;
using DockerContainerUpdateChecker.Models;
using Microsoft.Extensions.Options;

namespace DockerContainerUpdateChecker.Services;

public sealed class DockerContainerCatalog(
    DockerClient dockerClient,
    IOptions<MonitoringOptions> monitoringOptions) : IContainerCatalog
{
    private readonly DockerClient dockerClient = dockerClient;
    private readonly MonitoringOptions monitoringOptions = monitoringOptions.Value;

    public async Task<IReadOnlyList<ContainerDescriptor>> GetRunningContainersAsync(CancellationToken cancellationToken)
    {
        var containers = await dockerClient.Containers.ListContainersAsync(
            new ContainersListParameters { All = false },
            cancellationToken);

        var excluded = new HashSet<string>(monitoringOptions.ExcludedContainers ?? [], StringComparer.OrdinalIgnoreCase);
        var result = new List<ContainerDescriptor>();

        foreach (var container in containers)
        {
            var containerName = NormalizeContainerName(container);
            if (excluded.Contains(containerName) || excluded.Contains(container.ID))
            {
                continue;
            }

            if (!ImageReferenceParser.TryParse(container.Image, out var imageReference))
            {
                continue;
            }

            var imageDetails = await dockerClient.Images.InspectImageAsync(container.ImageID, cancellationToken);
            var localDigest = ResolveLocalDigest(imageReference, imageDetails.RepoDigests);

            result.Add(new ContainerDescriptor(
                container.ID,
                containerName,
                container.Image,
                imageReference,
                localDigest));
        }

        return result;
    }

    private static string NormalizeContainerName(ContainerListResponse container)
    {
        var name = container.Names?.FirstOrDefault() ?? container.ID;
        return name.Trim('/');
    }

    private static string? ResolveLocalDigest(ImageReference imageReference, IList<string>? repoDigests)
    {
        if (repoDigests is null || repoDigests.Count == 0)
        {
            return null;
        }

        var exactPrefix = $"{imageReference.RegistryRepository}@";
        var exactMatch = repoDigests.FirstOrDefault(digest => digest.StartsWith(exactPrefix, StringComparison.OrdinalIgnoreCase));
        var selected = exactMatch ?? repoDigests[0];
        var separatorIndex = selected.IndexOf('@');

        return separatorIndex >= 0 ? selected[(separatorIndex + 1)..] : null;
    }
}
