using Azure.Core;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;

namespace UiT.ChatUiT2.MaintenanceFunctions.Tools;
public static class ConfigTools
{
    public static IConfigurationBuilder AddAzureKeyVaultSecrets(IConfigurationBuilder config)
    {
        var builtConfig = config.Build();
        var keyvaultName = builtConfig["KeyVault:KeyVaultName"];

        if(string.IsNullOrEmpty(keyvaultName))
        {
            return config;
        }

        // DefaultAzureCredential will find and used the managed identity automatically
        SecretClientOptions options = new SecretClientOptions()
        {
            Retry =
            {
                Delay= TimeSpan.FromSeconds(2),
                MaxDelay = TimeSpan.FromSeconds(16),
                MaxRetries = 5,
                Mode = RetryMode.Exponential
            }
        };
        var client = new SecretClient(new Uri($"https://{builtConfig["KeyVault:KeyVaultName"]}.vault.azure.net/"),
                                        new DefaultAzureCredential(), options);
        config.AddAzureKeyVault(client, new KeyVaultSecretManager());

        return config;
    }
}
