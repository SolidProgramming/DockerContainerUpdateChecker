namespace DockerContainerUpdateChecker.Configuration;

public sealed class LocalizationOptions
{
    public const string SectionName = "Localization";

    public string Culture { get; set; } = "en-US";
}
