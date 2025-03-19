using ChatUiT2.Integration.Tests.Services;
using ChatUiT2.Services;
using Microsoft.Extensions.Configuration;

namespace ChatUiT2.Integration.Tests.TestStaging
{
    public static class ConfigurationStaging
    {
        public static IConfiguration GetConfiguration(string environment)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile($"appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddUserSecrets<RagDatabaseServiceCosmosDbNoSqlTests>()
                .Build();
            return config;
        }
        public static void AddToConfiguration(string environment, IConfigurationBuilder config)
        {
            config.AddJsonFile($"appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddUserSecrets<RagDatabaseServiceCosmosDbNoSqlTests>()
                .Build();
        }
    }
}
