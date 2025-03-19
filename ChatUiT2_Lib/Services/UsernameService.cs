using ChatUiT2.Interfaces;

namespace ChatUiT2.Services;
public class UsernameService : IUsernameService
{
    private IAuthUserService _authUserService { get; set; }

    public UsernameService(IAuthUserService authUserService)
    {
        _authUserService = authUserService;
    }

    public async Task<string> GetUsername()
    {
        return await _authUserService.GetUsername() ?? string.Empty;
    }
}
