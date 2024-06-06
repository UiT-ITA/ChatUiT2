namespace ChatUiT2.Interfaces;

public interface IKeyVaultService
{
    Task SetSecretAsync(string secretName, string secretValue);
    Task SetKeyForUser(string username, byte[] key);
    Task<byte[]> GetKeyAsync(string username);
    Task<string?> GetEndpointKey(string endpointName);
}
