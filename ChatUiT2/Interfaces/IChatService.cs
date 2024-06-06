using ChatUiT2.Models;
using ChatUiT2.Services;

namespace ChatUiT2.Interfaces;

public interface IChatService
{
    Task GetChatResponse(string? message);
    Task GetChatResponse(WorkItemChat chat, string? message);
}
