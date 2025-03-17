using ChatUiT2.Interfaces;
using MediatR;

namespace ChatUiT2.Models.Mediatr;

public class GetChatResponseAsStringRequestHandler : IRequestHandler<GetChatResponseAsStringRequest, string>
{
    private readonly IChatService _chatService;

    public GetChatResponseAsStringRequestHandler(IChatService chatService)
    {
        this._chatService = chatService;
    }
    public async Task<string> Handle(GetChatResponseAsStringRequest request, CancellationToken cancellationToken)
    {
        return await _chatService.GetChatResponseAsString(request.Chat);
    }
}
