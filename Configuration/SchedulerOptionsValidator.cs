using Cronos;
using Microsoft.Extensions.Options;

namespace DockerContainerUpdateChecker.Configuration;

public sealed class SchedulerOptionsValidator : IValidateOptions<SchedulerOptions>
{
    public ValidateOptionsResult Validate(string? name, SchedulerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Cron))
        {
            return ValidateOptionsResult.Fail("Scheduler:Cron is required.");
        }

        try
        {
            _ = CronExpression.Parse(options.Cron, CronFormat.Standard);
            return ValidateOptionsResult.Success;
        }
        catch (CronFormatException ex)
        {
            return ValidateOptionsResult.Fail($"Scheduler:Cron is invalid: {ex.Message}");
        }
    }
}
