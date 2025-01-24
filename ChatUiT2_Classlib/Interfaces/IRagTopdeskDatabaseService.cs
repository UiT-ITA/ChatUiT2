using ChatUiT2.Models;
using ChatUiT2_Classlib.Model;
using ChatUiT2_Classlib.Model.RagProject;
using ChatUiT2_Classlib.Model.Topdesk;
using Microsoft.AspNetCore.Components.Forms;
using MongoDB.Bson;
using OpenAI.Embeddings;

namespace ChatUiT2.Interfaces;

public interface IRagTopdeskDatabaseService
{
    Task DeleteTopdeskEmbedding(TopdeskTextEmbedding embedding);
    Task<List<TopdeskKnowledgeItem>> GetAllTopdeskKnowledgeItems(bool includeEmbeddings = false);
    Task<TopdeskKnowledgeItem> GetByTopdeskId(string topdeskId);
    Task<List<TopdeskTextEmbedding>> GetEmbeddingsByTopdeskKnowledgeItemId(string knowledgeItemId);
    Task<List<RagTextEmbedding>> GetEmbeddingsByItemId(RagProject ragProject, string itemId);
    Task SaveTopdeskKnowledgeItem(TopdeskKnowledgeItem topdeskKnowledgeItem);
    Task SaveTopdeskKnowledgeItemEmbedding(TopdeskTextEmbedding embedding);
    Task<string> GetTextResponseForChat(WorkItemChat chat);
    Task<OpenAIEmbedding> GetEmbeddingForText(string text);
    Task SetAllEmbeddingVectorsToNull();
    Task<List<TopdeskTextEmbedding>> GetAllEmbeddings();
    Task<List<RagSearchResult>> DoRagSearch(string searchTerm, int numResults = 3, double minMatchScore = 0.8);
    Task<QuestionsFromTextResult?> GenerateQuestionsFromContent(string content, int numToGenerateMin = 5, int numToGenerateMax = 20);
    Task SaveRagProject(RagProject ragProject);
    Task<RagProject> GetRagProjectById(string projectId, bool loadItems = false);
    Task<List<RagProject>> GetAllRagProjects();
    Task DeleteKnowledgeItem(TopdeskKnowledgeItem knowledgeItem);
    Task DeleteOrphanEmbeddings();
    Task DeleteRagProject(RagProject ragProject);
    Task<RagProject?> HandleRagProjectUpload(IBrowserFile file);
    Task<List<RagTextEmbedding>> GetEmbeddingsByProject(RagProject ragProject);
    Task SaveRagEmbedding(RagProject ragProject, RagTextEmbedding embedding);
    Task DeleteRagEmbedding(RagProject ragProject, RagTextEmbedding embedding);
}