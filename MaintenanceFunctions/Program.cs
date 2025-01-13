using ChatUiT2.Interfaces;
using ChatUiT2.Services;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UiT.AutomatedRecordingFunctions.Services;
using UiT.ChatUiT2.MaintenanceFunctions.Tools;
using UiT.CommonToolsLib.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureLogging((context, builder) =>
    {
        // To turn of EF sql statement logging
        builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
        builder.AddApplicationInsightsWebJobs(config =>
        {
            string instrumentationKey = context?.Configuration["APPINSIGHTS_INSTRUMENTATIONKEY"] ?? string.Empty;
            if (!string.IsNullOrEmpty(instrumentationKey))
            {
                config.InstrumentationKey = instrumentationKey;
            }
        });
    })
    .ConfigureAppConfiguration((context, builder) =>
    {
        builder.AddJsonFile(Path.Combine(context.HostingEnvironment.ContentRootPath, "appsettings.json"), optional: true, reloadOnChange: false)
               .AddJsonFile(Path.Combine(context.HostingEnvironment.ContentRootPath, $"appsettings.{context.HostingEnvironment.EnvironmentName}.json"), optional: true, reloadOnChange: false)
               .AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<Microsoft.ApplicationInsights.Extensibility.ITelemetryInitializer, MyTelemetryInitializer>(); // Add custom TelemetryInitializer
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddMemoryCache();
         
        services.AddSingleton<ITelemetryInitializer, MyTelemetryInitializer>();

        // Singleton services
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<IKeyVaultService, KeyVaultService>();
        services.AddSingleton<IEncryptionService, EncryptionService>();
        services.AddSingleton<AdminService>();

        // Scoped services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthUserService, AuthUserService>();
        services.AddScoped<IUpdateService, UpdateService>();
        services.AddScoped<SpeechService>();
        services.AddScoped<LocalStorageService>();

        // Transient services
        services.AddTransient<IDateTimeProvider, DateTimeProvider>();
    })
    .Build();

host.Run();

/// <summary>
/// The following class is to be able to add exclude from code coverage
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class Program
{
    public static void Main(string[] args)
    {
        // Your existing code
    }
}