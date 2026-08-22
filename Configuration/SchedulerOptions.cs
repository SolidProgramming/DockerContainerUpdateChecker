namespace DockerContainerUpdateChecker.Configuration;

public sealed class SchedulerOptions
{
    public const string SectionName = "Scheduler";

    public string Cron { get; set; } = "0 6 * * *";
}
