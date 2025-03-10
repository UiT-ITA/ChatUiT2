﻿using ChatUiT2.Interfaces;
using ChatUiT2.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChatUiT2.Integration.Tests.TestStaging;

public static class HostBuilderStaging
{
    public static IHost GetHost(string environment)
    {
        // Create a HostBuilder
        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((hostContext, config) =>
            {
                ConfigurationStaging.AddToConfiguration(environment, config);                    
            })
            .ConfigureLogging((hostContext, configLogging) =>
            {
                configLogging.ClearProviders();
                configLogging.AddConsole();
                configLogging.AddDebug();
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddMemoryCache();
                services.AddSingleton<IRagDatabaseService, RagDatabaseServiceCosmosDbNoSql>();
                services.AddTransient<IDateTimeProvider, DateTimeProvider>();
                services.AddSingleton<ISettingsService, SettingsService>();
            })
            .Build();
        return host;
    }
}
