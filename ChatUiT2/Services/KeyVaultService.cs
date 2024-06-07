using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using ChatUiT2.Interfaces;

namespace ChatUiT2.Services;

public class KeyVaultService : IKeyVaultService
{
    private readonly SecretClient _secretClient;
    private readonly IEncryptionService _encryptionService;

    public KeyVaultService(IConfiguration configuration,
                           IEncryptionService encryptionService)
    {
        var vaultUriString = configuration["ConnectionStrings:KeyVault"];
        if (vaultUriString == null)
        {
            throw new Exception("KeyVault not found");
        }
        var vaultUri = new Uri(vaultUriString);
        if (vaultUri == null)
        {
            throw new Exception("KeyVault not found");
        }
        _secretClient = new SecretClient(vaultUri, new DefaultAzureCredential());
        _encryptionService = encryptionService;
    }

    
    /// <summary>
    /// Get a secret from key vault
    /// </summary>
    /// <param name="secretName">The name of the secret in key vault</param>
    /// <returns>The value of the secret in key vault</returns>
    private async Task<string?> GetSecretAsync(string secretName)
    {
        try
        {
            KeyVaultSecret secret = await _secretClient.GetSecretAsync(secretName);
            return secret.Value;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Set a secret in key vault
    /// </summary>
    /// <param name="secretName">Name of secret in key vault</param>
    /// <param name="secretValue">Value to set for secret</param>
    /// <returns></returns>
    public async Task SetSecretAsync(string secretName, string secretValue)
    {
        try
        {
            Console.WriteLine($"Setting secret {secretName}");
            await _secretClient.SetSecretAsync(secretName, secretValue);
            Console.WriteLine($"Secret {secretName} set");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception during SetSecretAsync:");
            Console.WriteLine(ex);
            return;
        }
    }
    /// <summary>
    /// Calculates a key name from a username
    /// </summary>
    /// <param name="name">Username like aaa000@uit.no</param>
    /// <returns>The calculated key</returns>
    private string GetKeyFromUsername(string name)
    {
        return name.Replace("@", "at").Replace(".", "");
    }

    /// <summary>
    /// Sets encryption key for a user in the keyvault
    /// </summary>
    /// <param name="username">The username to set for</param>
    /// <param name="key">The secret encryption key</param>
    /// <returns></returns>
    public async Task SetKeyForUser(string username, byte[] key)
    {
        string keyName = GetKeyFromUsername(username);
        string aesKey = Convert.ToBase64String(key);

        // store the key in key vault
        await SetSecretAsync(keyName, aesKey);

    }

    /// <summary>
    /// Gets a users encryption key from the key vault
    /// Creates new if non found
    /// </summary>
    /// <param name="username">Username to get for</param>
    /// <returns></returns>
    public async Task<byte[]> GetKeyAsync(string username)
    {
        string keyName = GetKeyFromUsername(username);
        string? aesKey = await GetSecretAsync(keyName);
        if (aesKey == null)
        {
            // create new aes key
            Console.WriteLine($"Creating new key for {username}");
            var key = _encryptionService.GetRandomByteArray(32);
            var salt = _encryptionService.GetRandomByteArray(16);
            byte[] aesKeyBytes = _encryptionService.GetEncryptionKeyForAes256(key, salt, 50000);
            aesKey = Convert.ToBase64String(aesKeyBytes);

            // store the key in key vault
            await SetSecretAsync(keyName, aesKey);
        }
        // convert and return the key as a byte array
        return Convert.FromBase64String(aesKey);
    }

    //public async Task<byte[]> GetKeyAsync(string username)
    //{
    //    Console.WriteLine("WARNING! Using dummy keyvault");
    //    return Convert.FromBase64String("cJAmOFLkEg0u9xcL4dEKX5Ex3wiAieB1YqLu0 / wQIwI =");
    //}


    /// <summary>
    /// Gets the key for an endpoint
    /// </summary>
    /// <param name="endpointName">Name of the endpoint</param>
    /// <returns></returns>
    public async Task<string?> GetEndpointKey(string endpointName)
    {
        return await GetSecretAsync(endpointName);
    }

    public async Task<string?> GetDBString()
    {
        return await GetSecretAsync("MongoDB");
    }
}
