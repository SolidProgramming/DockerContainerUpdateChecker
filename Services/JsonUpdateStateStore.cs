using System.Text.Json;
using DockerContainerUpdateChecker.Configuration;
using DockerContainerUpdateChecker.Models;
using Microsoft.Extensions.Options;

namespace DockerContainerUpdateChecker.Services;

public sealed class JsonUpdateStateStore(IOptions<StorageOptions> storageOptions) : IUpdateStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string filePath = Path.Combine(storageOptions.Value.DataDirectory, "last-update-state.json");

    public async Task<PersistedUpdateState?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<PersistedUpdateState>(stream, SerializerOptions, cancellationToken);
    }

    public async Task SaveAsync(PersistedUpdateState state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
    }
}
