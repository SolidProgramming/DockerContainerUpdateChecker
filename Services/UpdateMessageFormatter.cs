using System.Text;
using System.Globalization;
using DockerContainerUpdateChecker.Models;
using System.Net;

namespace DockerContainerUpdateChecker.Services;

public sealed class UpdateMessageFormatter : IUpdateMessageFormatter
{
    public TelegramMessage Format(PersistedUpdateState? previous, UpdateCheckResult current)
    {
        var localCheckedAt = current.CheckedAtUtc.ToLocalTime();
        var localCheckedAtText = localCheckedAt.ToString("G", CultureInfo.CurrentCulture);
        var durationText = FormatDuration(current.Duration);
        var builder = new StringBuilder();
        var updateCount = current.Updates.Count;
        var warningCount = current.Errors.Count;

        if (updateCount == 0)
        {
            if (previous is null || previous.Updates.Count == 0)
            {
                builder.AppendLine("✅ <b>No updates available</b>");
            }
            else
            {
                builder.AppendLine("✅ <b>All monitored containers are up to date again</b>");
            }
        }
        else
        {
            builder.AppendLine("⬆️ <b>Container updates available</b>");
        }

        builder.AppendLine(
            $"<i>Checked:</i> {Encode(localCheckedAtText)} | <i>Scanned:</i> {current.CheckedContainerCount} | <i>Updates:</i> {current.UpdateCount} | <i>Up to date:</i> {current.UpToDateCount} | <i>Skipped:</i> {current.SkippedCount} | <i>Not checkable:</i> {current.NotCheckableCount} | <i>Errors:</i> {current.ErrorCount} | <i>Duration:</i> {Encode(durationText)}{FormatWarningCountSuffix(warningCount)}");

        if (updateCount > 0)
        {
            builder.AppendLine();
            foreach (var update in current.Updates.OrderBy(item => item.ContainerName, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine($"• <b>{Encode(update.ContainerName)}</b>");
                builder.AppendLine($"  <code>{Encode(update.ImageName)}</code>");
            }
        }

        if (warningCount > 0)
        {
            builder.AppendLine();
            builder.AppendLine("⚠️ <b>Warnings</b>");
            foreach (var error in current.Errors)
            {
                builder.AppendLine($"• {Encode(error)}");
            }
        }

        return new TelegramMessage(builder.ToString().TrimEnd(), "HTML");
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 1)
        {
            return $"{duration.TotalMilliseconds:F0} ms";
        }

        if (duration.TotalMinutes < 1)
        {
            return $"{duration.TotalSeconds:F1} s";
        }

        return $"{duration.TotalMinutes:F1} min";
    }

    private static string FormatWarningCountSuffix(int warningCount)
    {
        return warningCount > 0 ? $" | <i>Warnings:</i> {warningCount}" : string.Empty;
    }
}
