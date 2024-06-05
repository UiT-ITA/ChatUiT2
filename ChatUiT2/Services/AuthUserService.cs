using Microsoft.AspNetCore.Components.Authorization;

namespace ChatUiT2.Services;

public class AuthUserService(AuthenticationStateProvider AuthenticationStateProvider)
{
    public async Task<bool> TestInRole(string[] role)
    {
        var state = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        if (state.User.Identity is not null)
            if (state.User.Identity.IsAuthenticated)
            {
                foreach (var r in role)
                {
                    if (state.User.IsInRole(r))
                    {
                        return true;
                    }
                }
            }
        return false;
    }

    public async Task<string?> GetUsername()
    {
        var state = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        if (state.User.Identity is not null)
            if (state.User.Identity.IsAuthenticated)
            {
                return state.User.Identity.Name ?? null;
            }

        return null;
    }
}
