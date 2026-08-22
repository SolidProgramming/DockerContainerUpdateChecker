using DockerContainerUpdateChecker.Models;
using DockerContainerUpdateChecker.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DockerContainerUpdateChecker.Tests;

[TestClass]
public class UpdateMessageFormatterTests
{
    private static readonly UpdateMessageFormatter Formatter = new();

    [TestMethod]
    public void Format_RendersHtmlUpdateMessageForSingleContainer()
    {
        var result = new UpdateCheckResult(
            new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero),
            TimeSpan.FromSeconds(2.5),
            3,
            [
                new ContainerUpdateInfo("nginx", "nginx:latest", "sha256:old", "sha256:new")
            ],
            []);

        var message = Formatter.Format(null, result);

        Assert.AreEqual("HTML", message.ParseMode);
        StringAssert.Contains(message.Text, "⬆️ <b>Container updates available</b>");
        StringAssert.Contains(message.Text, "<b>nginx</b>");
        StringAssert.Contains(message.Text, "<code>nginx:latest</code>");
        StringAssert.Contains(message.Text, "<i>Scanned:</i> 3");
        StringAssert.Contains(message.Text, "<i>Updates:</i> 1");
    }

    [TestMethod]
    public void Format_RendersAllClearMessageWhenPreviousStateHadUpdates()
    {
        var previous = new PersistedUpdateState
        {
            Updates =
            [
                new PersistedUpdateEntry
                {
                    ContainerName = "nginx",
                    ImageName = "nginx:latest",
                    CurrentDigest = "sha256:old",
                    RemoteDigest = "sha256:new"
                }
            ]
        };

        var result = new UpdateCheckResult(
            new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero),
            TimeSpan.FromMilliseconds(850),
            4,
            [],
            []);

        var message = Formatter.Format(previous, result);

        Assert.AreEqual("HTML", message.ParseMode);
        StringAssert.Contains(message.Text, "✅ <b>All monitored containers are up to date again</b>");
        StringAssert.Contains(message.Text, "<i>Scanned:</i> 4");
        Assert.IsFalse(message.Text.Contains("Container updates available", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Format_IncludesWarningBlockWhenWarningsExist()
    {
        var result = new UpdateCheckResult(
            new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero),
            TimeSpan.FromSeconds(1),
            2,
            [],
            ["Registry lookup failed"]);

        var message = Formatter.Format(null, result);

        StringAssert.Contains(message.Text, "⚠️ <b>Warnings</b>");
        StringAssert.Contains(message.Text, "Registry lookup failed");
        StringAssert.Contains(message.Text, "<i>Warnings:</i> 1");
    }
}
