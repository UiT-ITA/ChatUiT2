using ChatUiT2.Models;
using OpenAI.Chat;

namespace ChatUiT2.Interfaces;
public interface IChatToolsService
{
    string GenerateImage(string description);
    Task<string> GetTopdesk(string query);
    Task<string> GetWebpage(string url);
    Task<string> GetWikipedia(string topic);
    Task<string> HandleToolCall(ChatToolCall toolCall);    
}