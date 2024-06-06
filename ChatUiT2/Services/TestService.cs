using ChatUiT2.Interfaces;

namespace ChatUiT2.Services;

public class TestService
{
    public IConfigService _configService { get; set; }
    public IAuthUserService _authUserService { get; set; }
    public IDatabaseService _databaseService { get; set; }
    public IUserService _userService { get; set; }
    public IKeyVaultService _keyVaultService { get; set; }

    public TestService(IConfigService configService, 
                IAuthUserService authUserService,
                IDatabaseService databaseService,
                IUserService userService,
                IKeyVaultService keyVaultService)
    {
        _configService = configService;
        _authUserService = authUserService;
        _databaseService = databaseService;
        _userService = userService;
        _keyVaultService = keyVaultService;
    }

    public void Test()
    {
        
    }
}
