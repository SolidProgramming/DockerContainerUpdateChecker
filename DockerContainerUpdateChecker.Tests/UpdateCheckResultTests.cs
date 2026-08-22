using DockerContainerUpdateChecker.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DockerContainerUpdateChecker.Tests;

[TestClass]
public class UpdateCheckResultTests
{
    [TestMethod]
    public void AggregatedCounts_AreCalculatedFromOutcomes()
    {
        var result = new UpdateCheckResult(
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(2),
            6,
            [
                new ContainerCheckOutcome("a", "a:latest", ContainerCheckStatus.UpdateAvailable, ContainerCheckReason.None),
                new ContainerCheckOutcome("b", "b:latest", ContainerCheckStatus.UpToDate, ContainerCheckReason.None),
                new ContainerCheckOutcome("c", "c:latest", ContainerCheckStatus.UpToDate, ContainerCheckReason.None),
                new ContainerCheckOutcome("d", "d:latest", ContainerCheckStatus.Skipped, ContainerCheckReason.Excluded),
                new ContainerCheckOutcome("e", "e:latest", ContainerCheckStatus.NotCheckable, ContainerCheckReason.DigestPinned),
                new ContainerCheckOutcome("f", "f:latest", ContainerCheckStatus.Error, ContainerCheckReason.None, "boom")
            ],
            [
                new ContainerUpdateInfo("a", "a:latest", "sha256:old", "sha256:new")
            ],
            ["boom"]);

        Assert.AreEqual(1, result.UpdateCount);
        Assert.AreEqual(2, result.UpToDateCount);
        Assert.AreEqual(1, result.SkippedCount);
        Assert.AreEqual(1, result.NotCheckableCount);
        Assert.AreEqual(1, result.ErrorCount);
    }
}
