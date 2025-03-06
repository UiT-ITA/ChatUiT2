using ChatUiT2.Models;
using ChatUiT2.Services;

namespace ChatUiT2.Interfaces;

public interface IChatService
{
    //Task GetChatResponse(WorkItemChat chat, string message);
    //Task GetChatResponse(WorkItemChat chat, ChatMessage message);
    Task GetChatResponse(WorkItemChat chat);
    Task<string> GetChatResponseAsString(WorkItemChat chat, AiModel? model = null);
}
