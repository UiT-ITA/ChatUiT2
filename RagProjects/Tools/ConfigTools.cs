using Azure.Core;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace UiT.RagProjects.Tools;

public static class ConfigTools
{
    public static IConfigurationBuilder AddAzureKeyVaultSecrets(IConfigurationBuilder config)
    {
        var builtConfig = config.Build();
        var x1 = builtConfig["KeyVault:KeyVaultName"];
        if(string.IsNullOrEmpty(x1))
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

    public static Dictionary<string, object> GetDataTestIdAttribute(string id)
    {
        return new Dictionary<string, object>
        {
            { "data-testid", id }
        };
    }
}
