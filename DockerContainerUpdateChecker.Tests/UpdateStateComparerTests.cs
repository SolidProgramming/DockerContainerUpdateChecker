using DockerContainerUpdateChecker.Models;
using DockerContainerUpdateChecker.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DockerContainerUpdateChecker.Tests;

[TestClass]
public class UpdateStateComparerTests
{
    private static readonly UpdateStateComparer Comparer = new();

    [TestMethod]
    public void HasChanged_ReturnsFalseForEquivalentUpdateSet()
    {
        var previous = new PersistedUpdateState
        {
            Updates =
            [
                new PersistedUpdateEntry
                {
                    ContainerName = "api",
                    ImageName = "example/api:latest",
                    CurrentDigest = "sha256:old",
                    RemoteDigest = "sha256:new"
                }
            ]
        };

        var current = new UpdateCheckResult(
            DateTimeOffset.UtcNow,
            [
                new ContainerUpdateInfo("api", "example/api:latest", "sha256:old", "sha256:new")
            ],
            []);

        Assert.IsFalse(Comparer.HasChanged(previous, current));
    }

    [TestMethod]
    public void HasChanged_ReturnsTrueWhenUpdatesDisappear()
    {
        var previous = new PersistedUpdateState
        {
            Updates =
            [
                new PersistedUpdateEntry
                {
                    ContainerName = "api",
                    ImageName = "example/api:latest",
                    CurrentDigest = "sha256:old",
                    RemoteDigest = "sha256:new"
                }
            ]
        };

        var current = new UpdateCheckResult(DateTimeOffset.UtcNow, [], []);

        Assert.IsTrue(Comparer.HasChanged(previous, current));
    }
}
