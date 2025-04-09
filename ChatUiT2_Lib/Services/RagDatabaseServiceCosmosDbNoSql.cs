using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using Microsoft.Azure.Cosmos;
using OpenAI.Embeddings;
using System.Text.Json;
using ChatUiT2.Models.RagProject;
using Microsoft.AspNetCore.Components.Forms;
using System.Text;
using ChatMessage = ChatUiT2.Models.ChatMessage;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using Ganss.Xss;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Collections.ObjectModel;
using Microsoft.Extensions.AI;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using MediatR;
using ChatUiT2.Models.Mediatr;
using ChatUiT2_Lib.Tools;

namespace ChatUiT2.Services;

public class RagDatabaseServiceCosmosDbNoSql : IRagDatabaseService, IDisposable
{
    private readonly IConfiguration _configuration;

    // Client
    private CosmosClient _cosmosClient;

    // CosmosDb defs
    private readonly string _ragProjectDefDbName = string.Empty;
    private readonly string _ragProjectDefContainerName = string.Empty;

    // Services
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISettingsService _settingsService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<RagDatabaseServiceCosmosDbNoSql> _logger;

    public RagDatabaseServiceCosmosDbNoSql(IConfiguration configuration,
                                           IDateTimeProvider dateTimeProvider,
                                           ISettingsService settingsService,
                                           IMemoryCache memoryCache,
                                           ILogger<RagDatabaseServiceCosmosDbNoSql> logger,
                                           CosmosClient cosmosClient)
    {
        this._configuration = configuration;
        this._dateTimeProvider = dateTimeProvider;
        this._settingsService = settingsService;
        this._memoryCache = memoryCache;
        this._logger = logger;
        this._ragProjectDefDbName = _configuration["RagProjectDefDatabaseName"] ?? string.Empty;
        this._ragProjectDefContainerName = _configuration["RagProjectDefContainerName"] ?? string.Empty;
        this._cosmosClient = cosmosClient;
    }

    private async Task<Container> GetRagProjectDefContainer()
    {
        // Create the project def database if it does not exist
        var ragProjectDefDatabase = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_ragProjectDefDbName);
        return (await ragProjectDefDatabase.Database.CreateContainerIfNotExistsAsync(_ragProjectDefContainerName, "/id")).Container;
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

        var containerProperties = new ContainerProperties(ragProject.Configuration.EmbeddingCollectioName, "/SourceItemId")
        {
            Id = ragProject.Configuration.EmbeddingCollectioName,
            PartitionKeyPath = "/SourceItemId",
            IndexingPolicy = new IndexingPolicy
            {                
                VectorIndexes = new Collection<VectorIndexPath>()
                {
                    new VectorIndexPath()
                    {
                        Path = "/Embedding",
                        Type = VectorIndexType.Flat
                    }
                }
            },            
            VectorEmbeddingPolicy = new VectorEmbeddingPolicy(new()
            {
                new()
                {
                    Path = "/Embedding",
                    DataType = VectorDataType.Float32,
                    DistanceFunction = DistanceFunction.Cosine,
                    Dimensions = 500                    
                }
            })
        };
        containerProperties.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/*" });
        containerProperties.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/Embedding/*" });
        return (await ragEmbeddingDatabase.Database.CreateContainerIfNotExistsAsync(containerProperties)).Container;
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
        var ragProjectContainer = await GetRagProjectDefContainer();
        var itemContainer = await GetItemContainer(ragProject);

        // Save the project definition
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            ragProject.Created = _dateTimeProvider.OffsetUtcNow;
            ragProject.Updated = _dateTimeProvider.OffsetUtcNow;
            // Check if the project already exists in the database with the given name
            var existingInDb = await GetRagProjectByName(ragProject.Name);
            if (existingInDb != null)
            {
                ragProject.Id = existingInDb.Id;
                await ragProjectContainer.UpsertItemAsync(ragProject, new PartitionKey(ragProject.Id));
            }
            else
            {
                ragProject.Id = Guid.NewGuid().ToString();
                await ragProjectContainer.CreateItemAsync(ragProject, new PartitionKey(ragProject.Id));
            }
        }
        else
        {
            ragProject.Updated = _dateTimeProvider.OffsetUtcNow;
            await ragProjectContainer.UpsertItemAsync(ragProject, new PartitionKey(ragProject.Id));
        }

        // Save the items in the specific db for this rag project
        foreach (var item in ragProject.ContentItems)
        {
            item.RagProjectId = ragProject.Id ?? string.Empty;
            var existingItem = await GetContentItemBySourceId(ragProject, item.SourceSystemId);            
            if (existingItem != null)
            {
                item.Id = existingItem.Id;
            }
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
        var ragProjectContainer = await GetRagProjectDefContainer();
        try
        {
            var response = await ragProjectContainer.ReadItemAsync<RagProject>(projectId, new PartitionKey(projectId));
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
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<RagProject?> GetRagProjectByName(string projectName, bool loadItems = false)
    {
        if (string.IsNullOrEmpty(projectName))
        {
            throw new ArgumentException("projectName must be set to get project");
        }
        var ragProjectContainer = await GetRagProjectDefContainer();
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.Name = @projectName")
                .WithParameter("@projectName", projectName);
            var iterator = ragProjectContainer.GetItemQueryIterator<RagProject>(query);
            var response = await iterator.ReadNextAsync();
            var ragProject = response.FirstOrDefault();
            
            if (ragProject == null)
            {
                // Not found
                return null;
            }

            // Get the items for the project
            if (loadItems)
            {
                var itemContainer = await GetItemContainer(ragProject);
                query = new QueryDefinition("SELECT * FROM c WHERE c.RagProjectId = @projectId")
                    .WithParameter("@projectId", ragProject.Id);
                var itemIterator = itemContainer.GetItemQueryIterator<ContentItem>(query);
                ragProject.ContentItems = new List<ContentItem>();
                while (iterator.HasMoreResults)
                {
                    var responseItems = await itemIterator.ReadNextAsync();
                    ragProject.ContentItems.AddRange(responseItems);
                }
            }

            return ragProject;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
    public async Task<List<RagProject>> GetAllRagProjects()
    {
        var ragProjectContainer = await GetRagProjectDefContainer();
        var result = new List<RagProject>();
        var query = "SELECT * FROM c";
        var iterator = ragProjectContainer.GetItemQueryIterator<RagProject>(new QueryDefinition(query));

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
        var ragProjectContainer = await GetRagProjectDefContainer();
        // Delete the item from the container
        await ragProjectContainer.DeleteItemAsync<RagProject>(ragProject.Id, new PartitionKey(ragProject.Id));

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

    /// <summary>
    /// Get a list of unique content item ids from the embeddings container.
    /// Can be used to find all items that has embeddings
    /// </summary>
    /// <param name="ragProject">The project to get for</param>
    /// <returns></returns>
    public async Task<List<string>> GetEmbeddingContentItemIdsByProject(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            throw new ArgumentException("Project id must be set to get embeddings");
        }

        var result = new List<string>();
        var embeddingContainer = await GetEmbeddingContainer(ragProject);
        var queryDefinition = new QueryDefinition("SELECT c.SourceItemId FROM c");
        var queryIterator = embeddingContainer.GetItemQueryIterator<RagTextEmbedding>(queryDefinition);

        while (queryIterator.HasMoreResults)
        {
            var response = await queryIterator.ReadNextAsync();
            result.AddRange(response.Select(x => x.SourceItemId));
        }

        return result;
    }

    /// <summary>
    /// Save a rag embedding to the rag project db
    /// If Id is null or empty it will create, or if forceCreateWithId is true.
    /// If id is set it will do update
    /// </summary>
    /// <param name="ragProject">The project the embedding belongs to</param>
    /// <param name="embedding">The embedding to save</param>
    /// <param name="forceCreateWithId">In cases where you want to create with a predefined id. For instance when copying a database</param>
    /// <returns></returns>
    public async Task SaveRagTextEmbedding(RagProject ragProject, RagTextEmbedding embedding, bool forceCreateWithId = false)
    {
        var embeddingContainer = await GetEmbeddingContainer(ragProject);

        if (string.IsNullOrEmpty(embedding.Id) || forceCreateWithId)
        {
            embedding.Created = _dateTimeProvider.OffsetUtcNow;
            embedding.Updated = _dateTimeProvider.OffsetUtcNow;
            // This is a new document, generate a new id unless forceCreateWithId true
            if(!forceCreateWithId)
            {
                embedding.Id = Guid.NewGuid().ToString();
            }
            await embeddingContainer.CreateItemAsync<RagTextEmbedding>(embedding, new PartitionKey(embedding.SourceItemId));
        }
        else
        {
            embedding.Updated = _dateTimeProvider.OffsetUtcNow;
            // This is an existing document, do update
            var partitionKey = new PartitionKey(embedding.SourceItemId);
            await embeddingContainer.ReplaceItemAsync<RagTextEmbedding>(embedding, embedding.Id, partitionKey);
        }
    }

    public async Task DeleteRagEmbedding(RagProject ragProject, RagTextEmbedding embedding)
    {
        if (string.IsNullOrEmpty(embedding.Id))
        {
            throw new ArgumentException("Embedding.Id must be set to delete embedding");
        }
        var embeddingContainer = await GetEmbeddingContainer(ragProject);

        var partitionKey = new PartitionKey(embedding.SourceItemId);
        try
        {
            await embeddingContainer.DeleteItemAsync<RagTextEmbedding>(embedding.Id, partitionKey);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Do nothing, the item was not found
        }
    }

    public async Task AddRagTextEmbedding(RagProject ragProject, 
                                          ContentItem item, 
                                          EmbeddingSourceType embedType,
                                          float[] embedding,
                                          string originalText = "")
    {
        if (ragProject == null)
        {
            throw new ArgumentException("ragProject must be set to add embedding");
        }
        if (string.IsNullOrEmpty(item?.Id))
        {
            throw new ArgumentException("itemId must be set to add embedding");
        }
        if (string.IsNullOrEmpty(originalText))
        {
            throw new ArgumentException("originalText must be set to add embedding");
        }
        RagTextEmbedding newEmbedding = new()
        {
            Model = _settingsService.EmbeddingModel.DeploymentName,
            ModelProvider = _settingsService.EmbeddingModel.DeploymentType.GetDisplayName(),
            Originaltext = originalText,
            SourceItemId = item.Id,
            RagProjectId = ragProject?.Id ?? string.Empty,
            TextType = embedType,
            ContentHash = HashTools.GetMd5Hash(item.StringForContentHash),
        };
        newEmbedding.Embedding = embedding;
        await SaveRagTextEmbedding(ragProject!, newEmbedding);
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

    public async Task<ContentItem?> GetContentItemBySourceId(RagProject ragProject, string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId))
        {
            throw new ArgumentException("sourceId must be set to get source item");
        }

        var itemContainer = await GetItemContainer(ragProject);
        var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.SourceSystemId = @id")
            .WithParameter("@id", sourceId);
        var queryIterator = itemContainer.GetItemQueryIterator<ContentItem>(queryDefinition);

        var response = await queryIterator.ReadNextAsync();
        var doc = response.FirstOrDefault();

        return doc;
    }

    public string GetItemContentString(ContentItem item)
    {
        string textContent = string.Empty;
        switch (item.ContentType)
        {
            case "INLINE":
                textContent = item.ContentText;
                break;
            default:
                break;
        }
        return textContent;
    }

    public Task DeleteContentItem(RagProject ragProject, ContentItem item)
    {
        throw new NotImplementedException();
    }

    public async Task<List<ContentItem>> GetContentItemsWithNoEmbeddings(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            throw new ArgumentException("ragProject.Id must be set to get content items");
        }

        var queryDefinition = new QueryDefinition("SELECT * FROM c");
        var itemContainer = await GetItemContainer(ragProject);
        var queryIterator = itemContainer.GetItemQueryIterator<ContentItem>(queryDefinition);

        var allContentItems = new List<ContentItem>();

        while (queryIterator.HasMoreResults)
        {
            var response = await queryIterator.ReadNextAsync();
            allContentItems.AddRange(response);
        }

        var allEmbeddings = await GetEmbeddingContentItemIdsByProject(ragProject);

        var resultContentItems = new List<ContentItem>();
        foreach (var item in allContentItems)
        {
            if (!allEmbeddings.Contains(item.Id))
            {
                resultContentItems.Add(item);
            }
        }

        return resultContentItems;
    }

    /// <summary>
    /// Get all content items for a project
    /// Using yield return to avoid loading all items in memory at once
    /// </summary>
    /// <param name="ragProject"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async IAsyncEnumerable<ContentItem> GetContentItemsByProject(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            throw new ArgumentException("ragProject.Id must be set to get content items");
        }

        var queryDefinition = new QueryDefinition("SELECT * FROM c");
        var itemContainer = await GetItemContainer(ragProject);
        var queryIterator = itemContainer.GetItemQueryIterator<ContentItem>(queryDefinition);

        var allContentItems = new List<ContentItem>();

        while (queryIterator.HasMoreResults)
        {
            var response = await queryIterator.ReadNextAsync();
            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async Task<int> GetNrOfContentItemsWithNoEmbeddings(RagProject ragProject)
    {
        return (await GetContentItemsWithNoEmbeddings(ragProject)).Count();
    }

    /// <summary>
    /// Sets all ContentItems in the project to EmbeddingsCreationInProgress = false
    /// This will be used to cancel all processing of embeddings for the project
    /// </summary>
    /// <returns></returns>
    public async Task DeleteAllEmbeddingEvents(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            throw new ArgumentException("ragProject.Id must be set to delete content items");
        }

        var container = await GetEmbeddingEventContainer(ragProject);
        var queryDefinition = new QueryDefinition("SELECT * FROM c");        
        var queryIterator = container.GetItemQueryIterator<EmbeddingEvent>(queryDefinition);
        var allEmbeddingEvents = new List<EmbeddingEvent>();
        while (queryIterator.HasMoreResults)
        {
            var response = await queryIterator.ReadNextAsync();
            allEmbeddingEvents.AddRange(response);
        }
        foreach (var item in allEmbeddingEvents)
        {            
            await container.DeleteItemAsync<EmbeddingEvent>(item.Id, new PartitionKey(item.RagProjectId));
        }
    }

    public async Task DeleteEmbeddingsForProject(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            throw new ArgumentException("ragProject.Id must be set to delete content items");
        }

        var container = await GetEmbeddingContainer(ragProject);
        var queryDefinition = new QueryDefinition("SELECT * FROM c");
        var queryIterator = container.GetItemQueryIterator<RagTextEmbedding>(queryDefinition);
        var allEmbeddings = new List<RagTextEmbedding>();
        while (queryIterator.HasMoreResults)
        {
            var response = await queryIterator.ReadNextAsync();
            allEmbeddings.AddRange(response);
        }
        foreach (var item in allEmbeddings)
        {
            await container.DeleteItemAsync<EmbeddingEvent>(item.Id, new PartitionKey(item.SourceItemId));
        }
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

    public async Task<EmbeddingEvent?> GetEmbeddingEventById(RagProject ragProject, string eventId)
    {
        if (string.IsNullOrEmpty(eventId))
        {
            throw new ArgumentException("eventId must be set to get embedding event");
        }

        var container = await GetEmbeddingEventContainer(ragProject);
        try
        {
            var response = await container.ReadItemAsync<EmbeddingEvent>(eventId, new PartitionKey(ragProject.Id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Handle the case where the item is not found
            return null;
        }
    }

    /// <summary>
    /// Gets EmbeddingEvent and makes sure to set the processing flag to avoid other threads
    /// from updating it.
    /// </summary>
    /// <param name="ragProject"></param>
    /// <param name="eventId"></param>
    /// <param name="simulateEtagChanged">Only used by integration tests to check lock mechanism</param>
    /// <returns></returns>
    ///     
    public async Task<EmbeddingEvent?> GetEmbeddingEventByIdForProcessing(RagProject ragProject, 
                                                                          string eventId,
                                                                          bool simulateEtagChanged = false)
    {
        
        var container = await GetEmbeddingEventContainer(ragProject);

        var embeddingEvent = await GetEmbeddingEventById(ragProject, eventId);
        if (embeddingEvent == null)
        {
            return null;
        }
        var etag = simulateEtagChanged ? "WrongEtagValue" : embeddingEvent.ETag;

        if (embeddingEvent.IsProcessing)
        {
            // Someone else is already processing this event
            return null;
        }

        embeddingEvent.IsProcessing = true;

        try
        {
            var requestOptions = new ItemRequestOptions { IfMatchEtag = etag };
            var updateResponse = await container.ReplaceItemAsync(embeddingEvent, eventId, new PartitionKey(ragProject.Id), requestOptions);

            if (updateResponse.StatusCode == HttpStatusCode.OK)
            {
                return await GetEmbeddingEventById(ragProject, eventId);
            }
            else
            {
                // Update failed
                return null;
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            // ETag mismatch, someone else updated the item
            return null;
        }
    }

    public async Task<string?> GetExistingEmbeddingEventId(RagProject ragProject, string contentItemId, EmbeddingSourceType type)
    {
        if (string.IsNullOrEmpty(contentItemId))
        {
            throw new ArgumentException("contentItemId must be set to get embedding event");
        }

        var container = await GetEmbeddingEventContainer(ragProject);

        var query = new QueryDefinition("SELECT c.id FROM c WHERE c.ContentItemId = @contentItemId AND c.EmbeddingSourceType = @type")
            .WithParameter("@contentItemId", contentItemId)
            .WithParameter("@type", type);

        using (var iterator = container.GetItemQueryIterator<EmbeddingEvent>(query))
        {
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var embeddingEvent = response.FirstOrDefault();
                if (embeddingEvent != null)
                {
                    return embeddingEvent.Id;
                }
            }
        }
        return null;
    }

    public async Task DeleteEmbeddingEvent(RagProject ragProject, EmbeddingEvent item)
    {
        if (item?.Id == null)
        {
            throw new ArgumentException("item must be set to delete EmbeddingEvent");
        }
        await DeleteEmbeddingEvent(ragProject, item.Id);
    }

    public async Task DeleteEmbeddingEvent(RagProject ragProject, string eventId)
    {
        if (string.IsNullOrEmpty(eventId))
        {
            throw new ArgumentException("eventId must be set to delete EmbeddingEvent");
        }

        var container = await GetEmbeddingEventContainer(ragProject);

        try
        {
            var response = await container.DeleteItemAsync<EmbeddingEvent>(eventId, new PartitionKey(ragProject.Id));
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                // Item successfully deleted
            }
            else
            {
                // Handle unsuccessful deletion
                throw new Exception($"Failed to delete EmbeddingEvent {eventId}, http status code {response.StatusCode}");
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Item not found
        }
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

    public async Task<IEnumerable<EmbeddingEvent>> GetEmbeddingEventsByProjectId(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            throw new ArgumentException("projectId must be set to get embedding event");
        }
        List<EmbeddingEvent> result = new List<EmbeddingEvent>();

        // Key not in cache, so get data.
        var container = await GetEmbeddingEventContainer(ragProject);

        var query = new QueryDefinition("SELECT * FROM c WHERE c.RagProjectId = @projectId")
            .WithParameter("@projectId", ragProject.Id);

        var iterator = container.GetItemQueryIterator<EmbeddingEvent>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            result.AddRange(response);
        }

        return result;
    }

    public async Task<List<string>> GetAllDatabaseIdsAsync()
    {
        var databaseList = new List<string>();
        var iterator = _cosmosClient.GetDatabaseQueryIterator<DatabaseProperties>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var database in response)
            {
                databaseList.Add(database.Id);
            }
        }

        return databaseList;
    }

    /// <summary>
    /// Get a list of unique embedding ids from the embeddings container.
    /// Can be used when for instance copying rag database to get a list of
    /// already existing embeddings.
    /// </summary>
    /// <param name="ragProject">The project to get for</param>
    /// <returns></returns>
    public async Task<List<string>> GetEmbeddingIdsByProject(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            throw new ArgumentException("Project id must be set to get embeddings");
        }

        var result = new List<string>();
        var embeddingContainer = await GetEmbeddingContainer(ragProject);
        var queryDefinition = new QueryDefinition("SELECT c.id FROM c");
        var queryIterator = embeddingContainer.GetItemQueryIterator<RagTextEmbedding>(queryDefinition);

        while (queryIterator.HasMoreResults)
        {
            var response = await queryIterator.ReadNextAsync();
            result.AddRange(response.Select(x => x.Id));
        }

        return result;
    }

    /// <summary>
    /// Deletes a database by Id
    /// </summary>
    /// <param name="id">The id of the database to delete</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task DeleteDatabase(string id)
    {
        try
        {
            var database = _cosmosClient.GetDatabase(id);
            await database.DeleteAsync();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Handle the case where the database does not exist
            Console.WriteLine($"Database with id '{id}' does not exist.");
            _logger.LogInformation("The database {databaseId} was not found. No delete operation performed.",
                                   id);
        }
    }

    public async Task<List<RagSearchResult>> DoGenericRagSearch(RagProject ragProject, float[] floatsUser, int numResults = 3, double minMatchScore = 0.8)
    {
        List<RagSearchResult> result = [];

        var embeddingContainer = await GetEmbeddingContainer(ragProject);
        var queryDef = new QueryDefinition(
            query: $"SELECT TOP {numResults} c.Originaltext as EmbeddingText, c.SourceItemId as SourceId, VectorDistance(c.Embedding,@embedding) AS MatchScore FROM c ORDER BY VectorDistance(c.Embedding,@embedding)"
        ).WithParameter("@embedding", floatsUser);

        using FeedIterator<RagSearchResult> feed = embeddingContainer.GetItemQueryIterator<RagSearchResult>(
            queryDefinition: queryDef
        );
        while (feed.HasMoreResults)
        {
            FeedResponse<RagSearchResult> response = await feed.ReadNextAsync();
            foreach (var item in response)
            {
                item.Source = ragProject.Name;
                result.Add(item);
            }
        }

        // Get details about ContentItem
        foreach (var res in result)
        {
            var contentItem = await GetContentItemById(ragProject, res.SourceId);
            if(contentItem == null)
            {
                continue;
            }
            res.ContentUrl = contentItem?.ViewUrl ?? string.Empty;
            res.SourceAltId = contentItem?.SourceSystemAltId ?? string.Empty;
            res.SourceContent = GetItemContentString(contentItem!);
            res.ContentTitle = contentItem?.Title ?? string.Empty;
        }

        return result;
    }

    public void Dispose()
    {
        _cosmosClient.Dispose();
    }
}