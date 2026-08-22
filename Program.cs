using Cronos;
using Docker.DotNet;
using DockerContainerUpdateChecker.Configuration;
using DockerContainerUpdateChecker.Jobs;
using DockerContainerUpdateChecker.Services;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.InMemory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var localSettingsPath = AppSettingsLocalBootstrapper.EnsureExists();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile(localSettingsPath, optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

builder.Services.AddSingleton<IValidateOptions<ServerOptions>, ServerOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<SchedulerOptions>, SchedulerOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<TelegramOptions>, TelegramOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<StorageOptions>, StorageOptionsValidator>();

builder.Services.AddOptions<ServerOptions>()
    .Bind(builder.Configuration.GetSection(ServerOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddOptions<SchedulerOptions>()
    .Bind(builder.Configuration.GetSection(SchedulerOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddOptions<TelegramOptions>()
    .Bind(builder.Configuration.GetSection(TelegramOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddOptions<MonitoringOptions>()
    .Bind(builder.Configuration.GetSection(MonitoringOptions.SectionName));

builder.Services.AddOptions<DockerOptions>()
    .Bind(builder.Configuration.GetSection(DockerOptions.SectionName));

builder.Services.AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
    .ValidateOnStart();

var configuredPort = builder.Configuration.GetValue<int?>($"{ServerOptions.SectionName}:Port") ?? 8080;
builder.WebHost.UseUrls($"http://0.0.0.0:{configuredPort}");

builder.Services.AddHttpClient(RegistryManifestClient.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("DockerContainerUpdateChecker/1.0");
});

builder.Services.AddHttpClient(TelegramNotifier.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddSingleton(serviceProvider =>
{
    var dockerOptions = serviceProvider.GetRequiredService<IOptions<DockerOptions>>().Value;
    var endpoint = DockerEndpointResolver.Resolve(dockerOptions.Endpoint);

    return new DockerClientConfiguration(endpoint).CreateClient();
});

builder.Services.AddSingleton<IDashboardAuthorizationFilter, AllowAllDashboardAuthorizationFilter>();
builder.Services.AddSingleton<IContainerCatalog, DockerContainerCatalog>();
builder.Services.AddSingleton<IRegistryManifestClient, RegistryManifestClient>();
builder.Services.AddSingleton<IUpdateStateStore, JsonUpdateStateStore>();
builder.Services.AddSingleton<ITelegramNotifier, TelegramNotifier>();
builder.Services.AddSingleton<IUpdateStateComparer, UpdateStateComparer>();
builder.Services.AddSingleton<IUpdateMessageFormatter, UpdateMessageFormatter>();
builder.Services.AddSingleton<IContainerUpdateChecker, ContainerUpdateChecker>();
builder.Services.AddSingleton<JobExecutionGate>();
builder.Services.AddSingleton<UpdateCheckJob>();

builder.Services.AddHangfire(configuration => configuration.UseInMemoryStorage());
builder.Services.AddHangfireServer();

var app = builder.Build();

var logger = app.Logger;
var timeProvider = app.Services.GetRequiredService<TimeProvider>();
var serverOptions = app.Services.GetRequiredService<IOptions<ServerOptions>>().Value;
var schedulerOptions = app.Services.GetRequiredService<IOptions<SchedulerOptions>>().Value;
var telegramOptions = app.Services.GetRequiredService<IOptions<TelegramOptions>>().Value;
var dockerOptions = app.Services.GetRequiredService<IOptions<DockerOptions>>().Value;
var storageOptions = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;
var schedulerExpression = CronExpression.Parse(schedulerOptions.Cron, CronFormat.Standard);
var resolvedDockerEndpoint = DockerEndpointResolver.Resolve(dockerOptions.Endpoint);
var startupLocalTime = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeProvider.LocalTimeZone);
var nextRunUtc = schedulerExpression.GetNextOccurrence(timeProvider.GetUtcNow().UtcDateTime, timeProvider.LocalTimeZone);
DateTimeOffset? nextRunLocal = nextRunUtc.HasValue
    ? TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(nextRunUtc.Value, DateTimeKind.Utc)), timeProvider.LocalTimeZone)
    : null;
var telegramNotifier = app.Services.GetRequiredService<ITelegramNotifier>();
var telegramBotIdentity = await telegramNotifier.GetBotIdentityAsync(CancellationToken.None);

logger.LogWarning("Application startup completed at local time {StartupLocalTime}.", startupLocalTime);
logger.LogWarning("Resolved local settings path: {LocalSettingsPath}", localSettingsPath);
logger.LogWarning("Configured HTTP port: {Port}", serverOptions.Port);
logger.LogWarning("Resolved container API endpoint: {DockerEndpoint}", resolvedDockerEndpoint);
logger.LogWarning("Configured Telegram chat id: {TelegramChatId}", telegramOptions.ChatId);
logger.LogWarning(
    "Telegram bot validation succeeded. Bot id: {TelegramBotId}, username: {TelegramBotUsername}, display name: {TelegramBotName}.",
    telegramBotIdentity.Id,
    telegramBotIdentity.Username ?? "<no-username>",
    telegramBotIdentity.FirstName);
logger.LogWarning("Sending Telegram startup test notification to chat id {TelegramChatId}.", telegramOptions.ChatId);
await telegramNotifier.SendAsync(
    $"Startup test notification sent at {startupLocalTime:yyyy-MM-dd HH:mm:ss zzz} local time.",
    CancellationToken.None);
logger.LogWarning("Telegram startup test notification sent successfully.");
logger.LogWarning("Configured storage data directory: {DataDirectory}", storageOptions.DataDirectory);
logger.LogWarning("Configured cron expression: {CronExpression}", schedulerOptions.Cron);

if (nextRunLocal.HasValue)
{
    logger.LogWarning("Next scheduled docker update check will run at local time {NextRunLocalTime}.", nextRunLocal.Value);
}
else
{
    logger.LogWarning("No next scheduled docker update check could be calculated for cron expression {CronExpression}.", schedulerOptions.Cron);
}

app.MapGet("/", () => Results.Redirect("/hangfire"));
app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [app.Services.GetRequiredService<IDashboardAuthorizationFilter>()]
});

RecurringJob.AddOrUpdate<UpdateCheckJob>(
    recurringJobId: "docker-update-check",
    methodCall: job => job.RunAsync(CancellationToken.None),
    cronExpression: schedulerOptions.Cron);

app.Run();

internal sealed class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}

internal static class AppSettingsLocalBootstrapper
{
    private const string PreferredConfigDirectory = "/config";

    public static string EnsureExists()
    {
        var localSettingsPath = ResolveLocalSettingsPath();
        if (File.Exists(localSettingsPath))
        {
            return localSettingsPath;
        }

        const string template = """
        {
          "Server": {
            "Port": 8080
          },
          "Scheduler": {
            "Cron": "0 6 * * *"
          },
          "Docker": {
            "Endpoint": ""
          },
          "Telegram": {
            "BotToken": "replace-me",
            "ChatId": "replace-me"
          },
          "Storage": {
            "DataDirectory": "/config/data"
          }
        }
        """;

        try
        {
            var directory = Path.GetDirectoryName(localSettingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(localSettingsPath, template + Environment.NewLine);
            Console.WriteLine($"Created default local settings file at '{localSettingsPath}'.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Failed to create default local settings file at '{localSettingsPath}': {ex.Message}");
        }

        return localSettingsPath;
    }

    private static string ResolveLocalSettingsPath()
    {
        return Directory.Exists(PreferredConfigDirectory)
            ? Path.Combine(PreferredConfigDirectory, "appsettings.Local.json")
            : Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json");
    }
}
