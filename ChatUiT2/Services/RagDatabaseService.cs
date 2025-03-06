using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
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

namespace ChatUiT2.Services;

public class RagDatabaseService : IRagDatabaseService
{
    private readonly IConfiguration _configuration;

    // Client
    private MongoClient _mongoClientRagDb;
    // Services
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISettingsService _settingsService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<RagDatabaseService> _logger;
    private readonly IChatService _chatService;

    // Collections
    private readonly IMongoCollection<BsonDocument> _topdeskKnowledgeItemCollection;
    private readonly IMongoCollection<BsonDocument> _topdeskKnowledgeItemEmbeddingCollection;
    private readonly IMongoCollection<BsonDocument> _ragProjectDefinitionsItemCollection;

    public RagDatabaseService(IConfiguration configuration,
                              IDateTimeProvider dateTimeProvider,
                              ISettingsService settingsService,
                              IMemoryCache memoryCache,
                              ILogger<RagDatabaseService> logger,
                              IChatService chatService)
    {
        this._configuration = configuration;
        this._dateTimeProvider = dateTimeProvider;
        this._settingsService = settingsService;
        this._memoryCache = memoryCache;
        this._logger = logger;
        this._chatService = chatService;

        // Init RAG database client
        var connectionString = configuration.GetConnectionString("MongoDbRagProjectDef");
        if(string.IsNullOrEmpty(connectionString) == false)
        {
            var client = new MongoClient(connectionString);
            _mongoClientRagDb = client;
            var ragDatabase = client.GetDatabase(configuration["RagProjectDefDatabaseName"]);
            // Old refactor and delete
            _topdeskKnowledgeItemCollection = ragDatabase.GetCollection<BsonDocument>("TopdeskKnowledgeItems");
            _topdeskKnowledgeItemEmbeddingCollection = ragDatabase.GetCollection<BsonDocument>("TopdeskKnowledgeItemEmbeddings");
            // Old end

            _ragProjectDefinitionsItemCollection = ragDatabase.GetCollection<BsonDocument>(configuration["RagProjectDefCollection"]);
        }
    }

    public async Task<OpenAIEmbedding> GetEmbeddingForText(string text)
    {
        string name;
        var model = _settingsService.EmbeddingModel;

        return await AzureOpenAIService.GetEmbedding(text, model, endpoint);
    }

    public async Task<List<RagTextEmbedding>> GetAllEmbeddingsMissingKnowledgeItem()
    {
        List<RagTextEmbedding> result = [];
        var documents = await _topdeskKnowledgeItemEmbeddingCollection.FindAsync(new BsonDocument());
        foreach (var doc in documents.ToList())
        {
            var embedding = BsonSerializer.Deserialize<RagTextEmbedding>(doc.AsBsonDocument);
            result.Add(embedding);
        }
        return result;

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
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);

        var itemCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.ItemCollectionName);

        // Save the project definition
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            ragProject.Created = _dateTimeProvider.OffsetUtcNow;
            ragProject.Updated = _dateTimeProvider.OffsetUtcNow;
            var document = ragProject.ToBsonDocument();
            // This is new document, generate new id
            var newId = ObjectId.GenerateNewId().ToString();
            document["_id"] = newId;
            await _ragProjectDefinitionsItemCollection.InsertOneAsync(document);
            ragProject.Id = newId;
        }
        else
        {
            ragProject.Updated = _dateTimeProvider.OffsetUtcNow;
            var document = ragProject.ToBsonDocument();
            // This is an existing document, do update
            var filter = Builders<BsonDocument>.Filter.Eq("_id", ragProject.Id);
            document.Remove("_id");
            await _ragProjectDefinitionsItemCollection.ReplaceOneAsync(filter, document);
        }

        // Save the items in the specific db for this rag project
        foreach (var item in ragProject.ContentItems)
        {
            item.RagProjectId = ragProject.Id ?? string.Empty;
            await SaveRagProjectItem(item, itemCollection);
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
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);

        var itemCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.ItemCollectionName);

        await SaveRagProjectItem(item, itemCollection);
    }

    public async Task SaveRagProjectItem(ContentItem item, IMongoCollection<BsonDocument> itemCollection)
    {
        if (string.IsNullOrEmpty(item.Id))
        {
            item.Created = _dateTimeProvider.OffsetUtcNow;
            item.Updated = _dateTimeProvider.OffsetUtcNow;
            var document = item.ToBsonDocument();
            // This is new document, generate new id
            document["_id"] = ObjectId.GenerateNewId().ToString();
            await itemCollection.InsertOneAsync(document);
        }
        else
        {
            item.Updated = _dateTimeProvider.OffsetUtcNow;
            var document = item.ToBsonDocument();
            // This is an existing document, do update
            var filter = Builders<BsonDocument>.Filter.Eq("_id", item.Id);
            document.Remove("_id");
            await itemCollection.ReplaceOneAsync(filter, document);
        }
    }

    public async Task<RagProject> GetRagProjectById(string projectId, bool loadItems = false)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            throw new ArgumentException("projectId must be set to get project");
        }
        var filter = Builders<BsonDocument>.Filter.Eq("_id", projectId);
        var documents = await _ragProjectDefinitionsItemCollection.FindAsync(filter);
        var ragProject = BsonSerializer.Deserialize<RagProject>(documents.FirstOrDefault().AsBsonDocument);

        // Get the items for the project
        if (loadItems)
        {
            var ragDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration?.DbName);
            var itemCollection = ragDatabase.GetCollection<BsonDocument>(ragProject.Configuration?.ItemCollectionName);
            var itemFilter = Builders<BsonDocument>.Filter.Eq("RagProjectId", projectId);
            var itemDocuments = await itemCollection.FindAsync(new BsonDocument());
            ragProject.ContentItems = new List<ContentItem>();
            foreach (var doc in itemDocuments.ToList())
            {
                var item = BsonSerializer.Deserialize<ContentItem>(doc.AsBsonDocument);
                ragProject.ContentItems.Add(item);
            }
        }

        return ragProject;
    }

    public async Task<List<RagProject>> GetAllRagProjects()
    {
        List<RagProject> result = [];
        var documents = await _ragProjectDefinitionsItemCollection.FindAsync(new BsonDocument());
        foreach (var doc in documents.ToList())
        {
            var ragProject = BsonSerializer.Deserialize<RagProject>(doc.AsBsonDocument);
            result.Add(ragProject);
        }
        return result;
    }

    public async Task DeleteOrphanEmbeddings(RagProject ragProject)
    {
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var embeddingCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.EmbeddingCollectioName);

        var pipeline = new[]
        {
            new BsonDocument("$lookup", new BsonDocument
            {
                { "from", ragProject.Configuration.EmbeddingCollectioName },
                { "localField", "SourceItemId" },
                { "foreignField", "_id" },
                { "as", "children_docs" }
            }),
            new BsonDocument("$match", new BsonDocument
            {
                { "children_docs", new BsonDocument("$eq", new BsonArray()) }
            })
        };

        var orphans = embeddingCollection.Aggregate<RagTextEmbedding>(pipeline).ToList();
        foreach (var orphan in orphans)
        {
            await DeleteRagEmbedding(ragProject, orphan);
        }
    }

    public async Task DeleteRagProject(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            throw new ArgumentException("Embedding.Id must be set to delete embedding");
        }
        if (string.IsNullOrEmpty(ragProject.Configuration?.DbName))
        {
            throw new ArgumentException("RagProject.Configuration.DbName must be set to save project");
        }

        var filter = Builders<BsonDocument>.Filter.Eq("_id", ragProject.Id);
        await _ragProjectDefinitionsItemCollection.DeleteOneAsync(filter);

        // Drop the specific rag database
        _mongoClientRagDb.DropDatabase(ragProject.Configuration.DbName);
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
        List<RagTextEmbedding> result = [];
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var embeddingCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.EmbeddingCollectioName);

        var documents = await embeddingCollection.FindAsync(new BsonDocument());

        foreach (var doc in documents.ToList())
        {
            var knowledgeItem = BsonSerializer.Deserialize<RagTextEmbedding>(doc.AsBsonDocument);
            result.Add(knowledgeItem);
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
    /// Save a rag embedding to the rag project db
    /// </summary>
    /// <param name="ragProject">The project the embedding belongs to</param>
    /// <param name="embedding">The embedding to save</param>
    /// <returns></returns>
    public async Task SaveRagEmbedding(RagProject ragProject, RagTextEmbedding embedding)
    {
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var embeddingCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.EmbeddingCollectioName);

        if (string.IsNullOrEmpty(embedding.Id))
        {
            embedding.Created = _dateTimeProvider.OffsetUtcNow;
            embedding.Updated = _dateTimeProvider.OffsetUtcNow;
            var document = embedding.ToBsonDocument();
            // This is new document, generate new id
            document["_id"] = ObjectId.GenerateNewId().ToString();
            await embeddingCollection.InsertOneAsync(document);
            embedding.Id = document["_id"].AsString;
        }
        else
        {
            embedding.Updated = _dateTimeProvider.OffsetUtcNow;
            var document = embedding.ToBsonDocument();
            // This is an existing document, do update
            var filter = Builders<BsonDocument>.Filter.Eq("_id", embedding.Id);
            document.Remove("_id");
            await embeddingCollection.ReplaceOneAsync(filter, document);
        }
    }

    public async Task DeleteRagEmbedding(RagProject ragProject, RagTextEmbedding embedding)
    {
        if (string.IsNullOrEmpty(embedding.Id))
        {
            throw new ArgumentException("Embedding.Id must be set to delete embedding");
        }
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var embeddingCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.EmbeddingCollectioName);

        var filter = Builders<BsonDocument>.Filter.Eq("_id", embedding.Id);
        await embeddingCollection.DeleteOneAsync(filter);
    }

    public async Task DeleteEmbeddingsForProject(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            throw new ArgumentException("ragProject.Id must be set to delete embeddings");
        }
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var embeddingCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.EmbeddingCollectioName);

        await embeddingCollection.DeleteManyAsync(new BsonDocument());
    }

    public async Task AddRagTextEmbedding(RagProject ragProject, string itemId, EmbeddingSourceType embedType, string originalText = "")
    {
        if (ragProject == null)
        {
            throw new ArgumentException("ragProject must be set to add embedding");
        }
        if (string.IsNullOrEmpty(itemId))
        {
            throw new ArgumentException("itemId must be set to add embedding");
        }
        if (string.IsNullOrEmpty(originalText))
        {
            throw new ArgumentException("originalText must be set to add embedding");
        }
        RagTextEmbedding newEmbedding = new()
        {
            Model = _settingsService.GetEmbeddingModel().Name,
            ModelProvider = _settingsService.GetEmbeddingModel().DeploymentType,
            Originaltext = originalText,
            SourceItemId = itemId,
            RagProjectId = ragProject?.Id ?? string.Empty,
            TextType = embedType
        };
        newEmbedding.Embedding = (await GetEmbeddingForText(newEmbedding.Originaltext)).ToFloats().ToArray();
        await SaveRagEmbedding(ragProject, newEmbedding);
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

    public async Task GenerateRagQuestionsFromContent(RagProject ragProject, ContentItem item)
    {
        try
        {
            string textContent = GetItemContentString(item);
            var questionsFromLlm = await GenerateQuestionsFromContent(textContent,
                                                                      ragProject.Configuration?.MinNumberOfQuestionsPerItem ?? 5,
                                                                      ragProject.Configuration?.MaxNumberOfQuestionsPerItem ?? 20);
            if (questionsFromLlm != null)
            {
                foreach (var question in questionsFromLlm.Questions)
                {
                    await AddRagTextEmbedding(ragProject, item.Id, EmbeddingSourceType.Question, question);
                }
            }
            else
            {
                throw new Exception("No questions generated by LLM");
            }
        }
        catch (Exception e)
        {
            throw new Exception($"Noe feilet ved generering av spørsmål for item {item.Id} {e.Message}");
        }
    }

    public async Task<string> SendRagSearchToLlm(List<RagSearchResult> ragSearchResults, string searchTerm)
    {
        AiModel defaultModel = _settingsService.GetDefaultModel();
        WorkItemChat chat = new();
        chat.Settings = new ChatSettings()
        {
            MaxTokens = defaultModel.MaxTokens,
            Model = defaultModel.DeploymentName,
            Temperature = 0.5f
        };
        chat.Type = WorkItemType.Chat;
        chat.Settings.Prompt = $"Use the information in the knowledge articles the user provides to answer the user question. Answer in the same language as the user is asking in.\n\n";
        for (int i = 0; i < ragSearchResults.Count(); i++)
        {
            chat.Messages.Add(new ChatMessage()
            {
                Role = ChatMessageRole.User,
                Content = $"## Knowledge article {i}\n\n{ragSearchResults.ElementAt(i).SourceContent}\n\n"
            });
        }

        chat.Messages.Add(new ChatMessage()
        {
            Role = ChatMessageRole.User,
            Content = $"My question is {searchTerm}"
        });

        return await _chatService.GetChatResponseAsString(chat);
    }

    public async Task<List<RagSearchResult>> DoGenericRagSearch(RagProject ragProject, string searchTerm, int numResults = 3, double minMatchScore = 0.8d)
    {
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var embeddingCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.EmbeddingCollectioName);

        List<RagSearchResult> result = [];
        var userPhraseEmbedding = await GetEmbeddingForText(searchTerm);

        var embeddings = await GetEmbeddingsByProject(ragProject);
        foreach (var embedding in embeddings)
        {
            var floatsUser = userPhraseEmbedding.ToFloats().ToArray();
            var floatsText = embedding.Embedding;
            if (floatsUser != null &&
                floatsText != null &&
                floatsUser.Length == floatsText.Length)
            {
                var matchScore = TensorPrimitives.CosineSimilarity(floatsUser, floatsText);
                RagSearchResult ragResult = new()
                {
                    MatchScore = matchScore,
                    SourceId = embedding.SourceItemId,
                    Source = ragProject.Name,
                    EmbeddingText = embedding.Originaltext

                };
                result.Add(ragResult);
            }
        }
        result = result.Where(x => x.MatchScore >= minMatchScore).ToList();
        if (result.Count() >= numResults)
        {
            result = result.OrderByDescending(x => x.MatchScore).Take(numResults).ToList();
        }
        else
        {
            result = result.OrderByDescending(x => x.MatchScore).ToList();
        }

        foreach (var res in result)
        {
            var contentItem = await GetContentItemById(ragProject, res.SourceId);
            res.ContentUrl = contentItem.ViewUrl;
            res.SourceAltId = contentItem.SourceSystemAltId;
            res.SourceContent = GetItemContentString(contentItem);
        }
        return result;
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
            var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
            var itemCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.ItemCollectionName);

            var filter = Builders<BsonDocument>.Filter.Eq("_id", itemId);
            var documents = await itemCollection.FindAsync(filter);
            var doc = documents.FirstOrDefault()?.AsBsonDocument;
            if (doc != null)
            {
                cachedValue = BsonSerializer.Deserialize<ContentItem>(doc);
                // Set cache options.
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5));

                // Save data in cache.
                _memoryCache.Set(cacheKey, cachedValue, cacheEntryOptions);
            }
        }

        return cachedValue;
    }
    public async Task DeleteContentItem(RagProject ragProject, ContentItem item)
    {
        if (string.IsNullOrEmpty(item.Id))
        {
            throw new ArgumentException("Item.Id must be set to delete contentItem");
        }
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var embeddingCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.EmbeddingCollectioName);
        var contentItemCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.ItemCollectionName);

        // Delete the content item
        var filter = Builders<BsonDocument>.Filter.Eq("_id", item.Id);
        await contentItemCollection.DeleteOneAsync(filter);

        // Delete related embeddings
        filter = Builders<BsonDocument>.Filter.Eq("SourceItemId", item.Id);
        await contentItemCollection.DeleteManyAsync(filter);
    }

    public async Task<List<ContentItem>> GetContentItemsWithNoEmbeddings(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            throw new ArgumentException("ragProject.Id must be set to delete contentItem");
        }
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var embeddingCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.EmbeddingCollectioName);
        var contentItemCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.ItemCollectionName);

        var pipeline = new[]
        {
            new BsonDocument("$lookup", new BsonDocument
            {
                { "from", ragProject.Configuration.EmbeddingCollectioName },
                { "localField", "_id" },
                { "foreignField", "SourceItemId" },
                { "as", "matchedItems" }
            }),
            new BsonDocument("$match", new BsonDocument
            {
                { "matchedItems", new BsonDocument("$eq", new BsonArray()) }
            })
        };

        var result = await contentItemCollection.Aggregate<ContentItem>(pipeline).ToListAsync();
        return result;
    }

    public async Task<int> GetNrOfContentItemsWithNoEmbeddings(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            throw new ArgumentException("ragProject.Id must be set to delete contentItem");
        }
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var embeddingCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.EmbeddingCollectioName);
        var contentItemCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.ItemCollectionName);

        var pipeline = new[]
        {
            new BsonDocument("$lookup", new BsonDocument
            {
                { "from", ragProject.Configuration.EmbeddingCollectioName },
                { "localField", "_id" },
                { "foreignField", "SourceItemId" },
                { "as", "matchedItems" }
            }),
            new BsonDocument("$match", new BsonDocument
            {
                { "matchedItems", new BsonDocument("$eq", new BsonArray()) }
            }),
            new BsonDocument("$count", "count")
        };

        var result = await contentItemCollection.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();
        return result != null ? result["count"].AsInt32 : 0;
    }

    /// <summary>
    /// Find out how many content items currently are marked as processing embeddings
    /// These are most likely waiting in the RabbitMq queue
    /// </summary>
    /// <param name="ragProject"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<long> GetNrOfContentItemsMarkedAsProcessingEmbeddings(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            throw new ArgumentException("ragProject.Id must be set to delete contentItem");
        }
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var contentItemCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.ItemCollectionName);
        var filter = Builders<BsonDocument>.Filter.Eq("EmbeddingsCreationInProgress", true);
        var count = await contentItemCollection.CountDocumentsAsync(filter);
        return count;
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
            throw new ArgumentException("ragProject.Id must be set to delete contentItem");
        }
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var embeddingEventCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.EmbeddingEventCollectioName);

        await embeddingEventCollection.DeleteManyAsync(new BsonDocument());
    }

    public string ReplaceHtmlLinebreaksWithNewline(string text)
    {
        // Regular expression to match all variants of <br> tags
        string pattern = @"<br\s*/?>";
        string result = Regex.Replace(text, pattern, "\n", RegexOptions.IgnoreCase);
        return result;
    }

    public string RemoveAllHtmlTagsFromString(string text)
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.KeepChildNodes = true;
        return sanitizer.Sanitize(text);
    }

    public IEnumerable<string> SplitTextIntoParagraphs(string text, bool removeHtmlTags = true, bool convertBrTagsToNewlines = true)
    {
        if (convertBrTagsToNewlines)
        {
            text = ReplaceHtmlLinebreaksWithNewline(text);
        }
        if (removeHtmlTags)
        {
            text = RemoveAllHtmlTagsFromString(text);
        }        
        string pattern = @"\n\s*\n";
        string strWithNormalizedDoubleNewline = Regex.Replace(text, pattern, "\n\n", RegexOptions.IgnoreCase);
        return strWithNormalizedDoubleNewline.Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
    }

    public async Task GenerateRagParagraphsFromContent(RagProject ragProject, ContentItem item, int minParagraphSize = 150)
    {
        try
        {
            string textContent = GetItemContentString(item);
            var paragraphs = SplitTextIntoParagraphs(textContent);
            foreach (var paragraph in paragraphs)
            {
                if (paragraph.Length < minParagraphSize)
                {
                    continue;
                }
                await AddRagTextEmbedding(ragProject, item.Id, EmbeddingSourceType.Paragraph, paragraph);
            }
        }
        catch (Exception e)
        {
            throw new Exception($"Noe feilet ved generering av paragraph embeddings for item {item.Id} {e.Message}");
        }
    }

    public async Task SaveRagEmbeddingEvent(RagProject ragProject, EmbeddingEvent embeddingEvent)
    {
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var embeddingCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.EmbeddingEventCollectioName);

        if (string.IsNullOrEmpty(embeddingEvent.Id))
        {
            embeddingEvent.Created = _dateTimeProvider.OffsetUtcNow;
            embeddingEvent.Updated = _dateTimeProvider.OffsetUtcNow;
            var document = embeddingEvent.ToBsonDocument();
            // This is new document, generate new id
            document["_id"] = ObjectId.GenerateNewId().ToString();
            await embeddingCollection.InsertOneAsync(document);
            embeddingEvent.Id = document["_id"].AsString;
        }
        else
        {
            embeddingEvent.Updated = _dateTimeProvider.OffsetUtcNow;
            var document = embeddingEvent.ToBsonDocument();
            // This is an existing document, do update
            var filter = Builders<BsonDocument>.Filter.Eq("_id", embeddingEvent.Id);
            document.Remove("_id");
            await embeddingCollection.ReplaceOneAsync(filter, document);
        }
    }

    /// <summary>
    /// Gets EmbeddingEvent and makes sure to set the processing flag to avoid other threads
    /// from updating it.
    /// </summary>
    /// <param name="ragProject"></param>
    /// <param name="eventId"></param>
    /// <returns></returns>
    public async Task<EmbeddingEvent?> GetEmbeddingEventByIdForProcessing(RagProject ragProject, string eventId)
    {
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var embeddingEventCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.EmbeddingEventCollectioName);

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", eventId),
            Builders<BsonDocument>.Filter.Eq("IsProcessing", false)
        );

        var update = Builders<BsonDocument>.Update.Set("IsProcessing", true);

        var result = await embeddingEventCollection.UpdateOneAsync(filter, update);
        if(result.ModifiedCount > 0)
        {
            return await GetEmbeddingEventById(ragProject, eventId);
        } else
        {
            // Someone else is already processing this event
            return null;
        }
    }

    public async Task<EmbeddingEvent?> GetEmbeddingEventById(RagProject ragProject, string eventId)
    {
        if (string.IsNullOrEmpty(eventId))
        {
            throw new ArgumentException("eventId must be set to get embedding event");
        }
        // Key not in cache, so get data.
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var embeddingEventCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.EmbeddingEventCollectioName);

        var filter = Builders<BsonDocument>.Filter.Eq("_id", eventId);
        var documents = await embeddingEventCollection.FindAsync(filter);
        return BsonSerializer.Deserialize<EmbeddingEvent>(documents.FirstOrDefault().AsBsonDocument);
    }

    public async Task<IEnumerable<EmbeddingEvent>> GetEmbeddingEventsByProjectId(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            throw new ArgumentException("projectId must be set to get embedding event");
        }
        List<EmbeddingEvent> result = [];
        // Key not in cache, so get data.
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var embeddingEventCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.EmbeddingEventCollectioName);

        var filter = Builders<BsonDocument>.Filter.Eq("RagProjectId", ragProject.Id);
        var documents = await embeddingEventCollection.FindAsync(filter);
        foreach (var doc in documents.ToList())
        {
            var embeddingEvent = BsonSerializer.Deserialize<EmbeddingEvent>(doc.AsBsonDocument);
            result.Add(embeddingEvent);
        }
        return result;
    }

    public async Task<bool> EmbeddingEventExists(RagProject ragProject, string contentItemId, EmbeddingSourceType type)
    {
        if (string.IsNullOrEmpty(contentItemId))
        {
            throw new ArgumentException("contentItemId must be set to get embedding event");
        }
        // Key not in cache, so get data.
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var embeddingEventCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.EmbeddingEventCollectioName);

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("ContentItemId", contentItemId),
            Builders<BsonDocument>.Filter.Eq("EmbeddingSourceType", type)
        );
        var documents = await embeddingEventCollection.FindAsync(filter);
        return documents.FirstOrDefault() != null;
    }

    public async Task DeleteEmbeddingEvent(RagProject ragProject, EmbeddingEvent item)
    {
        await DeleteEmbeddingEvent(ragProject, item.Id);
    }

    public async Task DeleteEmbeddingEvent(RagProject ragProject, string eventId)
    {
        if (string.IsNullOrEmpty(eventId))
        {
            throw new ArgumentException("eventId must be set to delete EmbeddingEvent");
        }
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var embeddingEventCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.EmbeddingEventCollectioName);

        var filter = Builders<BsonDocument>.Filter.Eq("_id", eventId);
        await embeddingEventCollection.DeleteOneAsync(filter);
    }

    /// <summary>
    /// Events that are older than the specified time will be returned
    /// </summary>
    /// <param name="ragProject"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<IEnumerable<EmbeddingEvent>> GetExpiredEmbeddingEvents(RagProject ragProject, int olderThanDays)
    {
        List<EmbeddingEvent> result = [];
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var embeddingEventCollection = ragItemsDatabase.GetCollection<EmbeddingEvent>(ragProject.Configuration.EmbeddingEventCollectioName);

        var query = embeddingEventCollection.AsQueryable().Where(x => x.Updated < _dateTimeProvider.UtcNow.AddDays(0 - olderThanDays));

        return query.ToList();
    }
}