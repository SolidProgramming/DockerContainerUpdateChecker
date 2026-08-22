using Cronos;
using Docker.DotNet;
using DockerContainerUpdateChecker.Configuration;
using DockerContainerUpdateChecker.Jobs;
using DockerContainerUpdateChecker.Services;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.InMemory;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

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

builder.Services.AddSingleton(_ =>
{
    var endpoint = OperatingSystem.IsWindows()
        ? new Uri("npipe://./pipe/docker_engine")
        : new Uri("unix:///var/run/docker.sock");

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

app.Run();

internal sealed class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}
