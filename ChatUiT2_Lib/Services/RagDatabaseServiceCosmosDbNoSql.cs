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

public class RagDatabaseServiceCosmosDbNoSql : IRagDatabaseService
{
    private readonly IConfiguration _configuration;

    // Client
    private CosmosClient _cosmosClient;

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
                                           ILogger<RagDatabaseService> logger)
    {
        this._configuration = configuration;
        this._dateTimeProvider = dateTimeProvider;
        this._settingsService = settingsService;
        this._memoryCache = memoryCache;
        this._logger = logger;
        this._chatService = new ChatService(null, this._settingsService, logger);

        var connectionString = configuration["ConnectionStrings:RagProjectDef"];
        if(string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentException("No connection string found for RagProjectDef");
        }
        _cosmosClient = new CosmosClient(connectionString);
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
        var ragProjectDefContainer = await GetRagProjectDefinitionContainer();
        var ragItemDatabase = await _cosmosClient.CreateDatabaseIfNotExistsAsync(ragProject.Configuration.DbName);
        var itemContainer = (await ragItemDatabase.Database.CreateContainerIfNotExistsAsync(ragProject.Configuration.ItemCollectionName, "/id")).Container;

        // Save the project definition
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            ragProject.Created = _dateTimeProvider.OffsetUtcNow;
            ragProject.Updated = _dateTimeProvider.OffsetUtcNow;
            ragProject.Id = Guid.NewGuid().ToString();
            await ragProjectDefContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
        }
        else
        {
            ragProject.Updated = _dateTimeProvider.OffsetUtcNow;
            await ragProjectDefContainer.UpsertItemAsync(ragProject, new PartitionKey(ragProject.Id));
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
        var database = _cosmosClient.GetDatabase(ragProject.Configuration.DbName);
        var itemContainer = database.GetContainer(ragProject.Configuration.ItemCollectionName);

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

    private async Task<Container> GetRagProjectDefinitionContainer()
    {
        var ragProjectDefDatabase = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_configuration["RagProjectDefCollection"]);
        return (await ragProjectDefDatabase.Database.CreateContainerIfNotExistsAsync(_configuration["RagProjectDefCollection"], "/id")).Container;
    }

    public async Task<RagProject> GetRagProjectById(string projectId, bool loadItems = false)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            throw new ArgumentException("projectId must be set to get project");
        }

        var ragProjectDefinitionsContainer = await GetRagProjectDefinitionContainer();
        var response = await ragProjectDefinitionsContainer.ReadItemAsync<RagProject>(projectId, new PartitionKey(projectId));
        var ragProject = response.Resource;

        // Get the items for the project
        if (loadItems)
        {
            var database = _cosmosClient.GetDatabase(ragProject.Configuration?.DbName);
            var itemContainer = database.GetContainer(ragProject.Configuration?.ItemCollectionName);
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

    public Task<List<RagProject>> GetAllRagProjects()
    {
        throw new NotImplementedException();
    }

    public Task DeleteOrphanEmbeddings(RagProject ragProject)
    {
        throw new NotImplementedException();
    }

    public Task DeleteRagProject(RagProject ragProject)
    {
        throw new NotImplementedException();
    }

    public Task<RagProject?> HandleRagProjectUpload(IBrowserFile file)
    {
        throw new NotImplementedException();
    }

    public Task<List<RagTextEmbedding>> GetEmbeddingsByProject(RagProject ragProject, bool withSourceItem = false)
    {
        throw new NotImplementedException();
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

    public Task<ContentItem?> GetContentItemById(RagProject ragProject, string itemId)
    {
        throw new NotImplementedException();
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

    public Task SaveRagEmbeddingEvent(RagProject ragProject, EmbeddingEvent embeddingEvent)
    {
        throw new NotImplementedException();
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
}