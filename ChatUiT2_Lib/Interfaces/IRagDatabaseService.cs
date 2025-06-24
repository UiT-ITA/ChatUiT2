using ChatUiT2.Models;
using ChatUiT2.Models.RagProject;
using Microsoft.AspNetCore.Components.Forms;
using OpenAI.Embeddings;

namespace ChatUiT2.Interfaces;

public interface IRagDatabaseService
{
    public Task SaveRagProject(RagProject ragProject, bool forceCreateWithId = false);
    public Task<RagProject?> GetRagProjectById(string projectId, bool loadItems = false);
    public Task<List<RagProject>> GetAllRagProjects();
    public Task DeleteOrphanEmbeddings(RagProject ragProject);
    public Task DeleteRagProject(RagProject ragProject);
    public Task<RagProject?> HandleRagProjectUpload(IBrowserFile file);
    public Task<List<RagTextEmbedding>> GetEmbeddingsByProject(RagProject ragProject, bool withSourceItem = false);
    public Task SaveRagTextEmbedding(RagProject ragProject, RagTextEmbedding embedding, bool forceCreateWithId = false);
    public Task DeleteRagEmbedding(RagProject ragProject, RagTextEmbedding embedding);
    public Task AddRagTextEmbedding(RagProject ragProject, 
                                    ContentItem item,
                                    EmbeddingSourceType embedType,
                                    float[] embedding, 
                                    string originalText = "");
    public Task<ContentItem?> GetContentItemById(RagProject ragProject, string itemId);
    public Task<List<RagSearchResult>> DoGenericRagSearch(RagProject ragProject, float[] floatsUser, int numResults = 3, double minMatchScore = 0.8);
    public Task DeleteContentItem(RagProject ragProject, ContentItem item);
    public Task<List<ContentItem>> GetContentItemsWithNoEmbeddings(RagProject ragProject);
    public Task SaveRagProjectItem(RagProject ragProject, ContentItem item);
    public Task<int> GetNrOfContentItemsWithNoEmbeddings(RagProject ragProject);
    public Task DeleteAllEmbeddingEvents(RagProject ragProject);
    public Task DeleteEmbeddingsForProject(RagProject ragProject);
    public Task SaveRagEmbeddingEvent(RagProject ragProject, EmbeddingEvent embeddingEvent);
    public Task<EmbeddingEvent?> GetEmbeddingEventById(RagProject ragProject, string eventId);    
    public Task<EmbeddingEvent?> GetEmbeddingEventByIdForProcessing(RagProject ragProject, string eventId, bool simulateEtagChanged = false);
    public Task<string?> GetExistingEmbeddingEventId(RagProject ragProject, string contentItemId, EmbeddingSourceType type);
    public Task DeleteEmbeddingEvent(RagProject ragProject, EmbeddingEvent item);
    public Task DeleteEmbeddingEvent(RagProject ragProject, string eventId);
    public Task<IEnumerable<EmbeddingEvent>> GetExpiredEmbeddingEvents(RagProject ragProject, int olderThanDays);
    public Task<bool> DatabaseExistsAsync(string databaseId);
    public Task<List<string>> GetEmbeddingContentItemIdsByProject(RagProject ragProject);
    public Task<IEnumerable<EmbeddingEvent>> GetEmbeddingEventsByProjectId(RagProject ragProject);
    public Task<List<string>> GetEmbeddingIdsByProject(RagProject ragProject);
    public Task<List<string>> GetAllDatabaseIdsAsync();
    public Task DeleteDatabase(string id);
    Task<RagProject?> GetRagProjectByName(string projectName, bool loadItems = false);
    string GetItemContentString(ContentItem item);
    Task<ContentItem?> GetContentItemBySourceId(RagProject ragProject, string sourceId);
    IAsyncEnumerable<ContentItem> GetContentItemsByProject(RagProject ragProject);
    IAsyncEnumerable<ContentItem> GetContentItemsWithUpdates(RagProject ragProject);
    Task<List<EmbeddingEvent>> GetEmbeddingEventsByItemId(RagProject ragProject, string contentItemId);
    Task<List<string>> GetItemSourceSystemIdsByProject(RagProject ragProject);
    Task<List<RagTextEmbedding>> GetEmbeddingsByItemId(RagProject ragProject, string contentItemId);
    Task<List<EmbeddingEvent>> GetEmbeddingEventsByProjectAsync(RagProject ragProject);
}