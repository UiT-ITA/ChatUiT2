using ChatUiT2.Models;
using OpenAI.Embeddings;

namespace ChatUiT2.Interfaces
{
    public interface IOpenAIService
    {
        Task<OpenAIEmbedding> GetEmbedding(string text);
        List<OpenAI.Chat.ChatMessage> GetOpenAiMessages(WorkItemChat chat, int maxTokens, bool allowImages = true);
        Task<string> GetResponse(WorkItemChat chat, bool allowFiles = false);
        Task GetStreamingResponse(WorkItemChat chat, ChatMessage responseMessage, bool allowImages = true);
        int GetTokens(ChatFile file);
        int GetTokens(string content);
        int GetTokens(WorkItemChat chat);
    }
}