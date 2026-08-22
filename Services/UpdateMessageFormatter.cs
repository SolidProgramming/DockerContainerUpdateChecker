using System.Text;
using System.Globalization;
using DockerContainerUpdateChecker.Models;

namespace DockerContainerUpdateChecker.Services;

public sealed class UpdateMessageFormatter : IUpdateMessageFormatter
{
    public string Format(PersistedUpdateState? previous, UpdateCheckResult current)
    {
        var localCheckedAt = current.CheckedAtUtc.ToLocalTime();
        var builder = new StringBuilder();
        builder.AppendLine($"Docker update check at {localCheckedAt.ToString("G", CultureInfo.CurrentCulture)} local time");
        builder.AppendLine();

        if (current.Updates.Count == 0)
        {
            builder.AppendLine(previous is null || previous.Updates.Count == 0
                ? "No updates are available."
                : "All monitored containers are up to date again.");
        }
        else
        {
            builder.AppendLine($"Updates available for {current.Updates.Count} container(s):");
            foreach (var update in current.Updates.OrderBy(item => item.ContainerName, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine($"- {update.ContainerName} ({update.ImageName})");
                builder.AppendLine($"  current: {update.CurrentDigest}");
                builder.AppendLine($"  remote : {update.RemoteDigest}");
            }
        }

        if (current.Errors.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Warnings:");
            foreach (var error in current.Errors)
            {
                builder.AppendLine($"- {error}");
            }
        }

        return builder.ToString().TrimEnd();
    }
}
