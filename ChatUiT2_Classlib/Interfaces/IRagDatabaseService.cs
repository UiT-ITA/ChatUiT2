using ChatUiT2.Models;
using ChatUiT2_Classlib.Model;
using ChatUiT2_Classlib.Model.RagProject;
using Microsoft.AspNetCore.Components.Forms;
using MongoDB.Bson;
using MongoDB.Driver;
using OpenAI.Embeddings;

namespace ChatUiT2.Interfaces;

public interface IRagDatabaseService
{
    public Task<string> GetTextResponseForChat(WorkItemChat chat);
    public Task<OpenAIEmbedding> GetEmbeddingForText(string text);
    public Task<QuestionsFromTextResult?> GenerateQuestionsFromContent(string content, int numToGenerateMin = 5, int numToGenerateMax = 20);
    public Task SaveRagProject(RagProject ragProject);
    public Task<RagProject> GetRagProjectById(string projectId, bool loadItems = false);
    public Task<List<RagProject>> GetAllRagProjects();
    public Task DeleteOrphanEmbeddings(RagProject ragProject);
    public Task DeleteRagProject(RagProject ragProject);
    public Task<RagProject?> HandleRagProjectUpload(IBrowserFile file);
    public Task<List<RagTextEmbedding>> GetEmbeddingsByProject(RagProject ragProject, bool withSourceItem = false);
    public Task SaveRagEmbedding(RagProject ragProject, RagTextEmbedding embedding);
    public Task DeleteRagEmbedding(RagProject ragProject, RagTextEmbedding embedding);
    public Task AddRagTextEmbedding(RagProject ragProject, string itemId, EmbeddingSourceType embedType, string originalText = "");
    public Task GenerateRagQuestionsFromContent(RagProject ragProject, ContentItem item);
    public Task<string> SendRagSearchToLlm(List<RagSearchResult> ragSearchResults, string searchTerm);
    public Task<ContentItem?> GetContentItemById(RagProject ragProject, string itemId);
    public string GetItemContentString(ContentItem item);
    public Task<List<RagSearchResult>> DoGenericRagSearch(RagProject ragProject, string searchTerm, int numResults = 3, double minMatchScore = 0.8);
    public Task DeleteContentItem(RagProject ragProject, ContentItem item);
    public Task<List<ContentItem>> GetContentItemsWithNoEmbeddings(RagProject ragProject);
    public Task SaveRagProjectItem(RagProject ragProject, ContentItem item);
    Task<int> GetNrOfContentItemsWithNoEmbeddings(RagProject ragProject);
    Task<long> GetNrOfContentItemsMarkedAsProcessingEmbeddings(RagProject ragProject);
    Task CancelAllEmbeddingProcessing(RagProject ragProject);
    Task DeleteEmbeddingsForProject(RagProject ragProject);
    Task GenerateRagParagraphsFromContent(RagProject ragProject, ContentItem item, int minParagraphSize = 150);
}