using ChatUiT2_Classlib.Model.Topdesk;

namespace ChatUiT2.Interfaces
{
    public interface IRagTopdeskDatabaseService
    {
        Task<List<TopdeskKnowledgeItem>> GetAllTopdeskKnowledgeItems();
        Task<TopdeskKnowledgeItem> GetByTopdeskId(string topdeskId);
        Task<List<TopdeskTextEmbedding>> GetEmbeddingsByTopdeskKnowledgeItemId(string knowledgeItemId);
        Task SaveTopdeskKnowledgeItem(TopdeskKnowledgeItem topdeskKnowledgeItem);
        Task SaveTopdeskKnowledgeItemEmbedding(TopdeskTextEmbedding embedding);
    }
}