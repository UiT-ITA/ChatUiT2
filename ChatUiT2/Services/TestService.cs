using ChatUiT2.Interfaces;
using ChatUiT2.Services;

namespace ChatUiT2.Services;

public class TestService
{
    public IConfiguration _configuration { get; set; }
    public ConfigService _configService { get; set; }
    public AuthUserService _authUserService { get; set; }
    public DatabaseService _databaseService { get; set; }
    public UserService _userService { get; set; }
    public KeyVaultService _keyVaultService { get; set; }

    public TestService(IConfiguration configuration,
                IConfigService configService, 
                IAuthUserService authUserService,
                IDatabaseService databaseService,
                IUserService userService,
                IKeyVaultService keyVaultService)
    {
        _configuration = configuration;
        _configService = (ConfigService)configService;
        _authUserService = (AuthUserService)authUserService;
        _databaseService = (DatabaseService)databaseService;
        _userService = (UserService)userService;
        _keyVaultService = (KeyVaultService)keyVaultService;

        Console.WriteLine("TestService created");
    }

    public void Test()
    {
        
    }
}
