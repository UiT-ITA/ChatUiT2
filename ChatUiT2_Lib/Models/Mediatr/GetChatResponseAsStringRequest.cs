using MediatR;

namespace ChatUiT2.Models.Mediatr;

public class GetChatResponseAsStringRequest : IRequest<string>
{
    public GetChatResponseAsStringRequest(WorkItemChat chat)
    {
        this.Chat = chat;
    }
    public WorkItemChat Chat { get; set; }
}
