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

AppSettingsLocalBootstrapper.EnsureExists();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
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
builder.Services.AddSingleton<TelegramTestJob>();

builder.Services.AddHangfire(configuration => configuration.UseInMemoryStorage());
builder.Services.AddHangfireServer();

var app = builder.Build();

_ = app.Services.GetRequiredService<IOptions<ServerOptions>>().Value;
var schedulerOptions = app.Services.GetRequiredService<IOptions<SchedulerOptions>>().Value;
_ = CronExpression.Parse(schedulerOptions.Cron, CronFormat.Standard);

app.MapGet("/", () => Results.Redirect("/hangfire"));
app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [app.Services.GetRequiredService<IDashboardAuthorizationFilter>()]
});

RecurringJob.AddOrUpdate<UpdateCheckJob>(
    recurringJobId: "docker-update-check",
    methodCall: job => job.RunAsync(CancellationToken.None),
    cronExpression: schedulerOptions.Cron);

RecurringJob.AddOrUpdate<TelegramTestJob>(
    recurringJobId: "telegram-test-notification",
    methodCall: job => job.RunAsync(CancellationToken.None),
    cronExpression: "0 0 1 1 *");

app.Run();

internal sealed class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}

internal static class AppSettingsLocalBootstrapper
{
    public static void EnsureExists()
    {
        var localSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json");
        if (File.Exists(localSettingsPath))
        {
            return;
        }

        const string template = """
        {
          "Server": {
            "Port": 8080
          },
          "Docker": {
            "Endpoint": ""
          },
          "Telegram": {
            "BotToken": "replace-me",
            "ChatId": "replace-me"
          },
          "Storage": {
            "DataDirectory": "/data"
          }
        }
        """;

        try
        {
            File.WriteAllText(localSettingsPath, template + Environment.NewLine);
            Console.WriteLine($"Created default local settings file at '{localSettingsPath}'.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Failed to create default local settings file at '{localSettingsPath}': {ex.Message}");
        }
    }
}
