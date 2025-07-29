using ChatUiT2.Interfaces;
using ChatUiT2.Services;
using Microsoft.Extensions.Configuration;

namespace ChatUiT2.Services;

public class TestService
{
    public IConfiguration _configuration { get; set; }
    public SettingsService _configService { get; set; }
    public AuthUserService _authUserService { get; set; }
    public DatabaseService _databaseService { get; set; }
    public UserService _userService { get; set; }
    public KeyVaultService _keyVaultService { get; set; }

    public TestService(IConfiguration configuration,
                ISettingsService configService, 
                IAuthUserService authUserService,
                IDatabaseService databaseService,
                IUserService userService,
                IKeyVaultService keyVaultService)
    {
        _configuration = configuration;
        _configService = (SettingsService)configService;
        _authUserService = (AuthUserService)authUserService;
        _databaseService = (DatabaseService)databaseService;
        _userService = (UserService)userService;
        _keyVaultService = (KeyVaultService)keyVaultService;

    }

    public void Test()
    {
        
    }
}
