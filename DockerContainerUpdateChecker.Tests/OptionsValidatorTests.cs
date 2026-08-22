using DockerContainerUpdateChecker.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DockerContainerUpdateChecker.Tests;

[TestClass]
public class OptionsValidatorTests
{
    [TestMethod]
    public void SchedulerValidator_RejectsInvalidCron()
    {
        var validator = new SchedulerOptionsValidator();
        var result = validator.Validate(null, new SchedulerOptions { Cron = "not-a-cron" });

        Assert.IsFalse(result.Succeeded);
    }

    [TestMethod]
    public void TelegramValidator_RejectsPlaceholderValues()
    {
        var validator = new TelegramOptionsValidator();
        var result = validator.Validate(null, new TelegramOptions
        {
            BotToken = "replace-me",
            ChatId = "replace-me"
        });

        Assert.IsFalse(result.Succeeded);
    }
}
