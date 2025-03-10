using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;

namespace ChatUiT2.Services.Template;

public class MyTelemetryInitializer : ITelemetryInitializer
{

    // Info:
    // https://learn.microsoft.com/en-us/azure/azure-monitor/app/api-filtering-sampling?tabs=javascriptwebsdkloaderscript#addmodify-properties-itelemetryinitializer

    private readonly string _AppName = "";
    private readonly string _AppDeveloper = "";
    private readonly string _AppEnvironment = "";

    public MyTelemetryInitializer(IConfiguration config) // IConfiguration config)
    {
        IConfigurationSection? appConfig = config?.GetSection("AppLogging");

        // Espens faste konfig av Logger
        _AppName = appConfig?.GetValue<string>("AppName") ?? string.Empty;
        _AppDeveloper = appConfig?.GetValue<string>("Developer") ?? string.Empty;
        _AppEnvironment = appConfig?.GetValue<string>("Environment") ?? string.Empty;
    }


    public void Initialize(ITelemetry telemetry)
    {
        switch (telemetry)
        {
            // Unngå at 404 havner i loggene, pga alle de som forsøker å hacke php. 
            case RequestTelemetry request when request.ResponseCode == "404":
                request.Success = true;
                break;
        }

        (telemetry as ISupportProperties)!.Properties["AppName"] = _AppName;
        (telemetry as ISupportProperties)!.Properties["AppDeveloper"] = _AppDeveloper;
        (telemetry as ISupportProperties)!.Properties["AppEnvironment"] = _AppEnvironment;
    }
}
