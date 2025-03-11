using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using Microsoft.Azure.Cosmos;
using OpenAI.Embeddings;
using System.Numerics.Tensors;
using System.Text.Json;
using ChatUiT2.Models.RagProject;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using System.Text;
using ChatMessage = ChatUiT2.Models.ChatMessage;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using Ganss.Xss;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using MongoDB.Driver.Core.Configuration;

namespace ChatUiT2.Services;

public class RagDatabaseServiceCosmosDbNoSql : IRagDatabaseService, IDisposable
{
    private readonly IConfiguration _configuration;

    // Client
    private CosmosClient _cosmosClient;

    // CosmosDb defs
    private readonly string _ragProjectDefDbName = string.Empty;
    private readonly string _ragProjectDefContainerName = string.Empty;
    private Database _ragProjectDefDatabase;
    private Container _ragProjectDefContainer;

    // Services
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISettingsService _settingsService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<RagDatabaseService> _logger;
    private readonly IChatService _chatService;

    public RagDatabaseServiceCosmosDbNoSql(IConfiguration configuration,
                                           IDateTimeProvider dateTimeProvider,
                                           ISettingsService settingsService,
                                           IMemoryCache memoryCache,
                                           ILogger<RagDatabaseService> logger,
                                           CosmosClient cosmosClient)
    {
        this._configuration = configuration;
        this._dateTimeProvider = dateTimeProvider;
        this._settingsService = settingsService;
        this._memoryCache = memoryCache;
        this._logger = logger;
        this._chatService = new ChatService(null, this._settingsService, logger);
        this._ragProjectDefDbName = _configuration["RagProjectDefDatabaseName"] ?? string.Empty;
        this._ragProjectDefContainerName = _configuration["RagProjectDefContainerName"] ?? string.Empty;
        this._cosmosClient = cosmosClient;
        InitializeAsync().GetAwaiter().GetResult();
    }

    public async Task InitializeAsync()
    {
        await InitializeCosmosDb(_ragProjectDefDbName);
    }

    private async Task InitializeCosmosDb(string databaseName)
    {
        // Create the project def database if it does not exist
        _ragProjectDefDatabase = await _cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
        _ragProjectDefContainer = (await _ragProjectDefDatabase.CreateContainerIfNotExistsAsync(_ragProjectDefContainerName, "/id")).Container;
    }

    public async Task<OpenAIEmbedding> GetEmbeddingForText(string text)
    {
        string name;
        var model = _settingsService.EmbeddingModel;

        return await _chatService.GetEmbedding(text, model);
    }

    public async Task<QuestionsFromTextResult?> GenerateQuestionsFromContent(string content, int numToGenerateMin = 5, int numToGenerateMax = 20)
    {
        AiModel gpt4MiniModel = _settingsService.GetModel("GPT-4o-Mini");
        WorkItemChat chat = new();
        chat.Settings = new ChatSettings()
        {
            MaxTokens = gpt4MiniModel.MaxTokens,
            Model = gpt4MiniModel.DeploymentName,
            Temperature = 0.5f
        };
        chat.Type = WorkItemType.Chat;
        chat.Settings.Prompt = $"Using the input that is a knowledge article, generate between {numToGenerateMin} and {numToGenerateMax} questions a person may ask that this article answers. Generate the questions in norwegian language. Give me the answer as json in the following format: {{ \"Questions\" : [ \"question1\", \"question2\" ] }}. Return the json string only no other information. Do not include ```json literal.";
        chat.Messages.Add(new ChatUiT2.Models.ChatMessage()
        {
            Role = ChatMessageRole.User,
            Content = content
        });
        var chatResponse = await _chatService.GetChatResponseAsString(chat);
        return JsonSerializer.Deserialize<QuestionsFromTextResult>(chatResponse);
    }

    private async Task<Container> GetItemContainer(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Configuration?.DbName))
        {
            throw new ArgumentException("RagProject.Configuration.DbName must be set to get container");
        }
        if (string.IsNullOrEmpty(ragProject.Configuration?.ItemCollectionName))
        {
            throw new ArgumentException("RagProject.Configuration.ItemCollectionName must be set to get container");
        }
        var ragItemDatabase = await _cosmosClient.CreateDatabaseIfNotExistsAsync(ragProject.Configuration.DbName);
        return (await ragItemDatabase.Database.CreateContainerIfNotExistsAsync(ragProject.Configuration.ItemCollectionName, "/id")).Container;
    }

    private async Task<Container> GetEmbeddingContainer(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Configuration?.DbName))
        {
            throw new ArgumentException("RagProject.Configuration.DbName must be set to get container");
        }
        if (string.IsNullOrEmpty(ragProject.Configuration?.EmbeddingCollectioName))
        {
            throw new ArgumentException("RagProject.Configuration.EmbeddingCollectioName must be set to get container");
        }
        var ragEmbeddingDatabase = await _cosmosClient.CreateDatabaseIfNotExistsAsync(ragProject.Configuration.DbName);
        return (await ragEmbeddingDatabase.Database.CreateContainerIfNotExistsAsync(ragProject.Configuration.EmbeddingCollectioName, "/SourceItemId")).Container;
    }

    private async Task<Container> GetEmbeddingEventContainer(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Configuration?.DbName))
        {
            throw new ArgumentException("RagProject.Configuration.DbName must be set to get container");
        }
        if (string.IsNullOrEmpty(ragProject.Configuration?.EmbeddingEventCollectioName))
        {
            throw new ArgumentException("RagProject.Configuration.EmbeddingEventCollectioName must be set to get container");
        }
        var ragEmbeddingEventDatabase = await _cosmosClient.CreateDatabaseIfNotExistsAsync(ragProject.Configuration.DbName);
        return (await ragEmbeddingEventDatabase.Database.CreateContainerIfNotExistsAsync(ragProject.Configuration.EmbeddingEventCollectioName, "/RagProjectId")).Container;
    }

    public async Task SaveRagProject(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Configuration?.DbName))
        {
            throw new ArgumentException("RagProject.Configuration.DbName must be set to save project");
        }
        if (string.IsNullOrEmpty(ragProject.Configuration?.ItemCollectionName))
        {
            throw new ArgumentException("RagProject.Configuration.ItemCollectionName must be set to save project");
        }
        var itemContainer = await GetItemContainer(ragProject);

        // Save the project definition
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            ragProject.Created = _dateTimeProvider.OffsetUtcNow;
            ragProject.Updated = _dateTimeProvider.OffsetUtcNow;
            ragProject.Id = Guid.NewGuid().ToString();
            await _ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        }
        else
        {
            ragProject.Updated = _dateTimeProvider.OffsetUtcNow;
            await _ragProjectDefContainer.UpsertItemAsync(ragProject, new PartitionKey(ragProject.Id));
        }

        // Save the items in the specific db for this rag project
        foreach (var item in ragProject.ContentItems)
        {
            item.RagProjectId = ragProject.Id ?? string.Empty;
            await SaveRagProjectItem(item, itemContainer);
        }
    }

    public async Task SaveRagProjectItem(RagProject ragProject, ContentItem item)
    {
        if (string.IsNullOrEmpty(ragProject.Configuration?.DbName))
        {
            throw new ArgumentException("RagProject.Configuration.DbName must be set to save project");
        }
        if (string.IsNullOrEmpty(ragProject.Configuration?.ItemCollectionName))
        {
            throw new ArgumentException("RagProject.Configuration.ItemCollectionName must be set to save project");
        }
        var itemContainer = await GetItemContainer(ragProject);

        await SaveRagProjectItem(item, itemContainer);
    }

    public async Task SaveRagProjectItem(ContentItem item, Container itemContainer)
    {
        if (string.IsNullOrEmpty(item.Id))
        {
            item.Created = _dateTimeProvider.OffsetUtcNow;
            item.Updated = _dateTimeProvider.OffsetUtcNow;
            item.Id = Guid.NewGuid().ToString();
            await itemContainer.CreateItemAsync(item, new PartitionKey(item.Id));
        }
        else
        {
            item.Updated = _dateTimeProvider.OffsetUtcNow;
            await itemContainer.UpsertItemAsync(item, new PartitionKey(item.Id));
        }
    }

    public async Task<RagProject?> GetRagProjectById(string projectId, bool loadItems = false)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            throw new ArgumentException("projectId must be set to get project");
        }

        var response = await _ragProjectDefContainer.ReadItemAsync<RagProject>(projectId, new PartitionKey(projectId));
        var ragProject = response.Resource;

        if (response.Resource == null)
        {
            // Not found
            return null;
        }

        // Get the items for the project
        if (loadItems)
        {
            var itemContainer = await GetItemContainer(ragProject);
            var query = new QueryDefinition("SELECT * FROM c WHERE c.RagProjectId = @projectId")
                .WithParameter("@projectId", projectId);
            var iterator = itemContainer.GetItemQueryIterator<ContentItem>(query);
            ragProject.ContentItems = new List<ContentItem>();
            while (iterator.HasMoreResults)
            {
                var responseItems = await iterator.ReadNextAsync();
                ragProject.ContentItems.AddRange(responseItems);
            }
        }

        return ragProject;
    }
    public async Task<List<RagProject>> GetAllRagProjects()
    {
        var result = new List<RagProject>();
        var query = "SELECT * FROM c";
        var iterator = _ragProjectDefContainer.GetItemQueryIterator<RagProject>(new QueryDefinition(query));

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            result.AddRange(response);
        }

        return result;
    }

    public Task DeleteOrphanEmbeddings(RagProject ragProject)
    {
        throw new NotImplementedException();
    }

    public async Task DeleteRagProject(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            throw new ArgumentException("RagProject.Id must be set to delete the project");
        }
        if (string.IsNullOrEmpty(ragProject.Configuration?.DbName))
        {
            throw new ArgumentException("RagProject.Configuration.DbName must be set to delete the project");
        }

        // Delete the item from the container
        await _ragProjectDefContainer.DeleteItemAsync<RagProject>(ragProject.Id, new PartitionKey(ragProject.Id));

        // Drop the specific database
        var database = _cosmosClient.GetDatabase(ragProject.Configuration.DbName);
        await database.DeleteAsync();
    }

    public async Task<RagProject?> HandleRagProjectUpload(IBrowserFile file)
    {
        using var stream = new MemoryStream();
        await file.OpenReadStream(50 * 1024 * 1024).CopyToAsync(stream);
        var fileContent = Encoding.UTF8.GetString(stream.ToArray());
        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true
        };
        var ragProject = JsonSerializer.Deserialize<RagProject>(fileContent, options);
        if (ragProject != null)
        {
            await SaveRagProject(ragProject);
            return ragProject;
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Get all embeddings for a rag project db
    /// </summary>
    /// <param name="ragProject">The project to get for</param>
    /// <returns></returns>
    public async Task<List<RagTextEmbedding>> GetEmbeddingsByProject(RagProject ragProject, bool withSourceItem = false)
    {
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            throw new ArgumentException("Project id must be set to get embeddings");
        }

        var result = new List<RagTextEmbedding>();
        var embeddingContainer = await GetEmbeddingContainer(ragProject);
        var queryDefinition = new QueryDefinition("SELECT * FROM c");
        var queryIterator = embeddingContainer.GetItemQueryIterator<RagTextEmbedding>(queryDefinition);

        while (queryIterator.HasMoreResults)
        {
            var response = await queryIterator.ReadNextAsync();
            result.AddRange(response);
        }

        if (withSourceItem)
        {
            foreach (var embedding in result)
            {
                var contentItem = await GetContentItemById(ragProject, embedding.SourceItemId);
                if (contentItem != null)
                {
                    embedding.ContentItem = contentItem;
                }
            }
        }

        return result;
    }

    public Task SaveRagEmbedding(RagProject ragProject, RagTextEmbedding embedding)
    {
        throw new NotImplementedException();
    }

    public Task DeleteRagEmbedding(RagProject ragProject, RagTextEmbedding embedding)
    {
        throw new NotImplementedException();
    }

    public Task AddRagTextEmbedding(RagProject ragProject, string itemId, EmbeddingSourceType embedType, string originalText = "")
    {
        throw new NotImplementedException();
    }

    public Task GenerateRagQuestionsFromContent(RagProject ragProject, ContentItem item)
    {
        throw new NotImplementedException();
    }

    public Task<string> SendRagSearchToLlm(List<RagSearchResult> ragSearchResults, string searchTerm)
    {
        throw new NotImplementedException();
    }

    public async Task<ContentItem?> GetContentItemById(RagProject ragProject, string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            throw new ArgumentException("itemId must be set to get source item");
        }

        string cacheKey = $"SourceItem_{itemId}";
        if (!_memoryCache.TryGetValue(cacheKey, out ContentItem? cachedValue))
        {
            // Key not in cache, so get data.
            var itemContainer = await GetItemContainer(ragProject);
            var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                .WithParameter("@id", itemId);
            var queryIterator = itemContainer.GetItemQueryIterator<ContentItem>(queryDefinition);

            var response = await queryIterator.ReadNextAsync();
            var doc = response.FirstOrDefault();
            if (doc != null)
            {
                cachedValue = doc;
                // Set cache options.
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5));

                // Save data in cache.
                _memoryCache.Set(cacheKey, cachedValue, cacheEntryOptions);
            }
        }

        return cachedValue;
    }

    public string GetItemContentString(ContentItem item)
    {
        throw new NotImplementedException();
    }

    public Task<List<RagSearchResult>> DoGenericRagSearch(RagProject ragProject, string searchTerm, int numResults = 3, double minMatchScore = 0.8)
    {
        throw new NotImplementedException();
    }

    public Task DeleteContentItem(RagProject ragProject, ContentItem item)
    {
        throw new NotImplementedException();
    }

    public Task<List<ContentItem>> GetContentItemsWithNoEmbeddings(RagProject ragProject)
    {
        throw new NotImplementedException();
    }

    public Task<int> GetNrOfContentItemsWithNoEmbeddings(RagProject ragProject)
    {
        throw new NotImplementedException();
    }

    public Task<long> GetNrOfContentItemsMarkedAsProcessingEmbeddings(RagProject ragProject)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAllEmbeddingEvents(RagProject ragProject)
    {
        throw new NotImplementedException();
    }

    public Task DeleteEmbeddingsForProject(RagProject ragProject)
    {
        throw new NotImplementedException();
    }

    public Task GenerateRagParagraphsFromContent(RagProject ragProject, ContentItem item, int minParagraphSize = 150)
    {
        throw new NotImplementedException();
    }

    public async Task SaveRagEmbeddingEvent(RagProject ragProject, EmbeddingEvent embeddingEvent)
    {
        var embeddingEventContainer = await GetEmbeddingEventContainer(ragProject);
        if (string.IsNullOrEmpty(embeddingEvent.Id))
        {
            embeddingEvent.Created = _dateTimeProvider.OffsetUtcNow;
            embeddingEvent.Updated = _dateTimeProvider.OffsetUtcNow;
            embeddingEvent.Id = Guid.NewGuid().ToString(); // Generate new ID
        }
        else
        {
            embeddingEvent.Updated = _dateTimeProvider.OffsetUtcNow;
        }
        await embeddingEventContainer.UpsertItemAsync(embeddingEvent, new PartitionKey(embeddingEvent.RagProjectId));
    }

    public Task<EmbeddingEvent?> GetEmbeddingEventById(RagProject ragProject, string eventId)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<EmbeddingEvent>> GetEmbeddingEventsByProjectId(RagProject ragProject)
    {
        throw new NotImplementedException();
    }

    public Task<EmbeddingEvent> GetEmbeddingEventByIdForProcessing(RagProject ragProject, string eventId)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetExistingEmbeddingEventId(RagProject ragProject, string contentItemId, EmbeddingSourceType type)
    {
        throw new NotImplementedException();
    }

    public Task DeleteEmbeddingEvent(RagProject ragProject, EmbeddingEvent item)
    {
        throw new NotImplementedException();
    }

    public Task DeleteEmbeddingEvent(RagProject ragProject, string eventId)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<EmbeddingEvent>> GetExpiredEmbeddingEvents(RagProject ragProject, int olderThanDays)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> DatabaseExistsAsync(string databaseId)
    {
        try
        {
            var database = _cosmosClient.GetDatabase(databaseId);
            await database.ReadAsync();
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _cosmosClient.Dispose();
    }
}