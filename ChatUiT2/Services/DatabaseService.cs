namespace ChatUiT2.Services;

public class DatabaseService
{
    private readonly KeyVaultService _keyVaultService;
    private readonly EncryptionService _encryptionService;

    public DatabaseService(IConfiguration configuration, EncryptionService encryptionService, KeyVaultService keyVaultService)
    {
        _keyVaultService = keyVaultService;
        _encryptionService = encryptionService;


    }
}
