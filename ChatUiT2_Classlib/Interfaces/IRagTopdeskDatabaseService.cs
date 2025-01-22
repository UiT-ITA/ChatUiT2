using ChatUiT2.Models;
using ChatUiT2_Classlib.Model;
using ChatUiT2_Classlib.Model.Topdesk;
using MongoDB.Bson;
using OpenAI.Embeddings;

namespace ChatUiT2.Interfaces
{
    public interface IRagTopdeskDatabaseService
    {
        Task DeleteTopdeskEmbedding(TopdeskTextEmbedding embedding);
        Task<List<TopdeskKnowledgeItem>> GetAllTopdeskKnowledgeItems(bool includeEmbeddings = false);
        Task<TopdeskKnowledgeItem> GetByTopdeskId(string topdeskId);
        Task<List<TopdeskTextEmbedding>> GetEmbeddingsByTopdeskKnowledgeItemId(string knowledgeItemId);
        Task SaveTopdeskKnowledgeItem(TopdeskKnowledgeItem topdeskKnowledgeItem);
        Task SaveTopdeskKnowledgeItemEmbedding(TopdeskTextEmbedding embedding);
        Task<string> GetTextResponseForChat(WorkItemChat chat);
        Task<OpenAIEmbedding> GetEmbeddingForText(string text);
        Task SetAllEmbeddingVectorsToNull();
        Task<List<TopdeskTextEmbedding>> GetAllEmbeddings();
        Task<List<RagSearchResult>> DoRagSearch(string searchTerm, int numResults = 3, double minMatchScore = 0.8);
        Task<QuestionsFromTextResult?> GenerateQuestionsFromContent(string content, int numToGenerateMin = 5, int numToGenerateMax = 20);
    }
}