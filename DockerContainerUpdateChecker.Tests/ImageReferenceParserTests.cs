using DockerContainerUpdateChecker.Models;
using DockerContainerUpdateChecker.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DockerContainerUpdateChecker.Tests;

[TestClass]
public class ImageReferenceParserTests
{
    [TestMethod]
    public void TryParse_UsesDockerHubDefaultsForShortImageName()
    {
        var parsed = ImageReferenceParser.TryParse("nginx:latest", out var imageReference);

        Assert.IsTrue(parsed);
        Assert.AreEqual(
            new ImageReference("docker.io", "library/nginx", "latest", "nginx:latest", "docker.io/library/nginx", false),
            imageReference);
    }

    [TestMethod]
    public void TryParse_SupportsCustomRegistryAndRepository()
    {
        var parsed = ImageReferenceParser.TryParse("ghcr.io/acme/demo:1.2.3", out var imageReference);

        Assert.IsTrue(parsed);
        Assert.AreEqual("ghcr.io", imageReference.Registry);
        Assert.AreEqual("acme/demo", imageReference.Repository);
        Assert.AreEqual("1.2.3", imageReference.Tag);
    }

    [TestMethod]
    public void TryParse_RecognizesDigestPinnedImage()
    {
        var parsed = ImageReferenceParser.TryParse("redis@sha256:abc123", out var imageReference);

        Assert.IsTrue(parsed);
        Assert.IsTrue(imageReference.IsDigestPinned);
        Assert.AreEqual("latest", imageReference.Tag);
    }
}
