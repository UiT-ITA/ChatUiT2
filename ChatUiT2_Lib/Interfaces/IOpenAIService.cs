using ChatUiT2.Models;
using OpenAI.Embeddings;
using OpenAI.Chat;

namespace ChatUiT2.Interfaces
{
    public interface IOpenAIService
    {
        Task<OpenAIEmbedding> GetEmbedding(string text);
        List<OpenAI.Chat.ChatMessage> GetOpenAiMessages(WorkItemChat chat, int maxTokens, bool allowImages = true);
        Task<string> GetResponse(WorkItemChat chat, bool allowFiles = false);
        Task<string> GetResponseRaw(List<OpenAI.Chat.ChatMessage> messages, ChatCompletionOptions options);
        Task GetStreamingResponse(WorkItemChat chat, ChatUiT2.Models.ChatMessage responseMessage, bool allowImages = true);
        IAsyncEnumerable<StreamingChatCompletionUpdate> GetStreamingResponseRaw(List<OpenAI.Chat.ChatMessage> messages, ChatCompletionOptions options);
        int GetTokens(ChatFile file);
        int GetTokens(string content);
        int GetTokens(WorkItemChat chat);
    }
}
