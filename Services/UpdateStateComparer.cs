using DockerContainerUpdateChecker.Models;

namespace DockerContainerUpdateChecker.Services;

public sealed class UpdateStateComparer : IUpdateStateComparer
{
    public bool HasChanged(PersistedUpdateState? previous, UpdateCheckResult current)
    {
        var previousFingerprints = previous?.Updates
            .Select(CreateFingerprint)
            .OrderBy(fingerprint => fingerprint, StringComparer.Ordinal)
            .ToArray() ?? [];

        var currentFingerprints = current.Updates
            .Select(CreateFingerprint)
            .OrderBy(fingerprint => fingerprint, StringComparer.Ordinal)
            .ToArray();

        return !previousFingerprints.SequenceEqual(currentFingerprints, StringComparer.Ordinal);
    }

    public PersistedUpdateState CreateState(UpdateCheckResult current)
    {
        return new PersistedUpdateState
        {
            LastCheckedAtUtc = current.CheckedAtUtc,
            Updates = current.Updates
                .Select(update => new PersistedUpdateEntry
                {
                    ContainerName = update.ContainerName,
                    ImageName = update.ImageName,
                    CurrentDigest = update.CurrentDigest,
                    RemoteDigest = update.RemoteDigest
                })
                .OrderBy(update => update.ContainerName, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static string CreateFingerprint(PersistedUpdateEntry update)
    {
        return $"{update.ContainerName}|{update.ImageName}|{update.CurrentDigest}|{update.RemoteDigest}";
    }

    private static string CreateFingerprint(ContainerUpdateInfo update)
    {
        return $"{update.ContainerName}|{update.ImageName}|{update.CurrentDigest}|{update.RemoteDigest}";
    }
}
