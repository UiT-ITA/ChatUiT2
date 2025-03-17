using ChatUiT2.Interfaces;
using ChatUiT2.Services;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Embeddings;

namespace ChatUiT2.Models.Mediatr;
public class GetUsernameRequestHandler : IRequestHandler<GetUsernameRequest, string>
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public GetUsernameRequestHandler(AuthenticationStateProvider authenticationStateProvider)
    {
        this._authenticationStateProvider = authenticationStateProvider;
    }
    public async Task<string> Handle(GetUsernameRequest request, CancellationToken cancellationToken)
    {
        

        try
        {
            var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
            if (state.User.Identity is not null)
                if (state.User.Identity.IsAuthenticated)
                {
                    return state.User.Identity.Name ?? null;
                }

            return null;
        }
        catch (Exception e)
        {
            return string.Empty;
        }
    }
}
