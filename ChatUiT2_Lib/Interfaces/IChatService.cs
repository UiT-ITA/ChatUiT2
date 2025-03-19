using ChatUiT2.Models;
using ChatUiT2.Services;
using OpenAI.Embeddings;

namespace ChatUiT2.Interfaces;

public interface IChatService
{
    //Task GetChatResponse(WorkItemChat chat, string message);
    //Task GetChatResponse(WorkItemChat chat, ChatMessage message);
    Task GetChatResponse(WorkItemChat chat);
    Task<OpenAIEmbedding> GetEmbedding(string text, AiModel model);
}
