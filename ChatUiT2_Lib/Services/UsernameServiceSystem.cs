using ChatUiT2.Interfaces;

namespace ChatUiT2.Services;

/// <summary>
/// Service to get the system username
/// When running as azure function there is no user context
/// In that case use this service that just returns a system username
/// </summary>
public class UsernameServiceSystem : IUsernameService
{
    public static readonly string SystemUsername = "System";
    public UsernameServiceSystem()
    {
    }

    public async Task<string> GetUsername()
    {
        return await Task.FromResult(SystemUsername);
    }
}
