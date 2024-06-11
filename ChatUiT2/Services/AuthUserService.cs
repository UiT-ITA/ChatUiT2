using Microsoft.AspNetCore.Components.Authorization;
using ChatUiT2.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using MongoDB.Bson;

namespace ChatUiT2.Services;

public class AuthUserService(AuthenticationStateProvider AuthenticationStateProvider) : IAuthUserService
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

    public async Task<string?> GetName()
    {

        var state = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        if (state.User.Identity is not null)
            if (state.User.Identity.IsAuthenticated)
            {
                //Console.WriteLine("Authenticated");
                //state.User.Claims.ToList().ForEach(c => Console.WriteLine(c.Type + " " + c.Value));
                //Console.WriteLine(state.User.Identities.ToJson());
                return state.User.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
            }
        return null;

    }
}
