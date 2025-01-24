using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using ChatUiT2_Classlib.Model;
using ChatUiT2_Classlib.Model.Topdesk;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using OpenAI.Embeddings;
using UiT.CommonToolsLib.Services;
using UiT.RestClientTopdesk.Model;
using System.Numerics.Tensors;
using System.Text.Json;
using ChatUiT2_Classlib.Model.RagProject;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using System.Text;
using Microsoft.IdentityModel.Tokens;
namespace ChatUiT2.Services;

public class RagTopdeskDatabaseService : IRagTopdeskDatabaseService
{
    private readonly IConfiguration _configuration;

    // Client
    private MongoClient _mongoClientRagDb;
    // Services
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IConfigService _configService;

    // Collections
    private readonly IMongoCollection<BsonDocument> _topdeskKnowledgeItemCollection;
    private readonly IMongoCollection<BsonDocument> _topdeskKnowledgeItemEmbeddingCollection;
    private readonly IMongoCollection<BsonDocument> _ragProjectDefinitionsItemCollection;

    public RagTopdeskDatabaseService(IConfiguration configuration,
                                     IDateTimeProvider dateTimeProvider,
                                     IConfigService configService)
    {
        this._configuration = configuration;
        this._dateTimeProvider = dateTimeProvider;
        this._configService = configService;

        // Init RAG database client
        var connectionString = configuration.GetConnectionString("MongoDbRagProjectDef");
        var client = new MongoClient(connectionString);
        _mongoClientRagDb = client;
        var ragDatabase = client.GetDatabase(configuration["RagProjectDefDatabaseName"]);
        // Old refactor and delete
        _topdeskKnowledgeItemCollection = ragDatabase.GetCollection<BsonDocument>("TopdeskKnowledgeItems");
        _topdeskKnowledgeItemEmbeddingCollection = ragDatabase.GetCollection<BsonDocument>("TopdeskKnowledgeItemEmbeddings");
        // Old end

        _ragProjectDefinitionsItemCollection = ragDatabase.GetCollection<BsonDocument>(configuration["RagProjectDefCollection"]);
    }


    /// <summary>
    /// Get topdesk knowledgeItems from the database
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    public async Task<List<TopdeskKnowledgeItem>> GetAllTopdeskKnowledgeItems(bool includeEmbeddings = false)
    {
        List<TopdeskKnowledgeItem> result = [];
        var documents = await _topdeskKnowledgeItemCollection.FindAsync(new BsonDocument());

        foreach (var doc in documents.ToList())
        {
            var knowledgeItem = BsonSerializer.Deserialize<TopdeskKnowledgeItem>(doc.AsBsonDocument);
            result.Add(knowledgeItem);
        }

        if (includeEmbeddings)
        {
            var allEmbeddings = await GetAllEmbeddings();
            foreach (var item in result)
            {
                item.Embeddings = allEmbeddings.Where(x => x.TopdeskKnowledgeItemId == item.TopdeskId).ToList();
            }
        }

        return result;
    }

    public async Task SaveTopdeskKnowledgeItem(TopdeskKnowledgeItem topdeskKnowledgeItem)
    {
        if (string.IsNullOrEmpty(topdeskKnowledgeItem.Id))
        {
            topdeskKnowledgeItem.Created = _dateTimeProvider.OffsetUtcNow;
            topdeskKnowledgeItem.Updated = _dateTimeProvider.OffsetUtcNow;
            var document = topdeskKnowledgeItem.ToBsonDocument();
            // This is new document, generate new id
            document["_id"] = ObjectId.GenerateNewId().ToString();
            await _topdeskKnowledgeItemCollection.InsertOneAsync(document);
        }
        else
        {
            topdeskKnowledgeItem.Updated = _dateTimeProvider.OffsetUtcNow;
            var document = topdeskKnowledgeItem.ToBsonDocument();
            // This is an existing document, do update
            var filter = Builders<BsonDocument>.Filter.Eq("_id", topdeskKnowledgeItem.Id);
            document.Remove("_id");
            await _topdeskKnowledgeItemCollection.ReplaceOneAsync(filter, document);
        }
    }

    /// <summary>
    /// Get topdesk knowledgeItem by topdesk id
    /// </summary>
    /// <param name="topdeskId">The knowledgeItem id in topdesk</param>
    /// <returns></returns>
    public async Task<TopdeskKnowledgeItem> GetByTopdeskId(string topdeskId)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("TopdeskId", topdeskId);
        var documents = await _topdeskKnowledgeItemCollection.FindAsync(filter);

        var knowledgeItem = BsonSerializer.Deserialize<TopdeskKnowledgeItem>(documents.FirstOrDefault().AsBsonDocument);

        return knowledgeItem;
    }

    /// <summary>
    /// Get topdesk embeddings for knowledgeItem topdesk id
    /// </summary>
    /// <param name="topdeskId">The knowledgeItem id in topdesk</param>
    /// <returns></returns>
    public async Task<List<TopdeskTextEmbedding>> GetEmbeddingsByTopdeskKnowledgeItemId(string knowledgeItemId)
    {
        List<TopdeskTextEmbedding> result = [];
        var filter = Builders<BsonDocument>.Filter.Eq("TopdeskKnowledgeItemId", knowledgeItemId);
        var documents = await _topdeskKnowledgeItemEmbeddingCollection.FindAsync(filter);

        foreach (var doc in documents.ToList())
        {
            var knowledgeItem = BsonSerializer.Deserialize<TopdeskTextEmbedding>(doc.AsBsonDocument);
            result.Add(knowledgeItem);
        }

        return result;
    }

    public async Task SaveTopdeskKnowledgeItemEmbedding(TopdeskTextEmbedding embedding)
    {
        if (string.IsNullOrEmpty(embedding.Id))
        {
            embedding.Created = _dateTimeProvider.OffsetUtcNow;
            embedding.Updated = _dateTimeProvider.OffsetUtcNow;
            var document = embedding.ToBsonDocument();
            // This is new document, generate new id
            document["_id"] = ObjectId.GenerateNewId().ToString();
            await _topdeskKnowledgeItemEmbeddingCollection.InsertOneAsync(document);
            embedding.Id = document["_id"].AsString;
        }
        else
        {
            embedding.Updated = _dateTimeProvider.OffsetUtcNow;
            var document = embedding.ToBsonDocument();
            // This is an existing document, do update
            var filter = Builders<BsonDocument>.Filter.Eq("_id", embedding.Id);
            document.Remove("_id");
            await _topdeskKnowledgeItemEmbeddingCollection.ReplaceOneAsync(filter, document);
        }
    }

    public async Task DeleteTopdeskEmbedding(TopdeskTextEmbedding embedding)
    {
        if (string.IsNullOrEmpty(embedding.Id))
        {
            throw new ArgumentException("Embedding.Id must be set to delete embedding");
        }
        var filter = Builders<BsonDocument>.Filter.Eq("_id", embedding.Id);
        await _topdeskKnowledgeItemEmbeddingCollection.DeleteOneAsync(filter);
    }

    public async Task DeleteKnowledgeItem(TopdeskKnowledgeItem knowledgeItem)
    {
        if (string.IsNullOrEmpty(knowledgeItem.Id))
        {
            throw new ArgumentException("KnowledgeItem.Id must be set to delete knowledgeItem");
        }
        var filter = Builders<BsonDocument>.Filter.Eq("_id", knowledgeItem.Id);
        await _topdeskKnowledgeItemCollection.DeleteOneAsync(filter);
        // Delete all embeddings for this knowledgeItem
        filter = Builders<BsonDocument>.Filter.Eq("TopdeskKnowledgeItemId", knowledgeItem.TopdeskId);
        await _topdeskKnowledgeItemEmbeddingCollection.DeleteManyAsync(filter);
    }

    /// <summary>
    /// For when you have a chat and you simply want to get a text response
    /// with the next answer from the model.
    /// </summary>
    /// <param name="chat">The chat to send to model</param>
    /// <returns></returns>
    public async Task<string> GetTextResponseForChat(WorkItemChat chat)
    {
        string name;
        var model = _configService.GetDefaultModel();
        var endpoint = _configService.GetEndpoint(model.Deployment);

        return await AzureOpenAIService.GetResponse(chat, model, endpoint);
    }

    public async Task<OpenAIEmbedding> GetEmbeddingForText(string text)
    {
        string name;
        var model = _configService.GetEmbeddingModel();
        var endpoint = _configService.GetEndpoint(model.Deployment);

        return await AzureOpenAIService.GetEmbedding(text, model, endpoint);
    }

    /// <summary>
    /// For the topdesk embedding collection set embedding field to null to clear all embeddings
    /// </summary>
    public async Task SetAllEmbeddingVectorsToNull()
    {
        var update = Builders<BsonDocument>.Update.Set("Embedding", BsonNull.Value);
        await _topdeskKnowledgeItemEmbeddingCollection.UpdateManyAsync(MongoDB.Driver.FilterDefinition<BsonDocument>.Empty, update);
    }

    public async Task<List<TopdeskTextEmbedding>> GetAllEmbeddings()
    {
        List<TopdeskTextEmbedding> result = [];
        var documents = await _topdeskKnowledgeItemEmbeddingCollection.FindAsync(new BsonDocument());
        foreach (var doc in documents.ToList())
        {
            var embedding = BsonSerializer.Deserialize<TopdeskTextEmbedding>(doc.AsBsonDocument);
            result.Add(embedding);
        }
        return result;
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

    public async Task<List<RagSearchResult>> DoRagSearch(string searchTerm, int numResults = 3, double minMatchScore = 0.8d)
    {
        List<RagSearchResult> result = [];
        var userPhraseEmbedding = await GetEmbeddingForText(searchTerm);

        var embeddings = await GetAllEmbeddings();
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
                    SourceId = embedding.TopdeskKnowledgeItemId,
                    Source = RagSources.Topdesk,
                    EmbeddingText = embedding.Originaltext,
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
            var knowledgeItem = await GetByTopdeskId(res.SourceId);
            res.ContentUrl = $"https://uit.topdesk.net/solutions/open-knowledge-items/item/{knowledgeItem.Number}/no".Replace(" ", "%20");
            res.SourceAltId = knowledgeItem.Number;
            res.SourceContent = knowledgeItem.Content;
        }
        return result;
    }

    public async Task<QuestionsFromTextResult?> GenerateQuestionsFromContent(string content, int numToGenerateMin = 5, int numToGenerateMax = 20)
    {
        Model gpt4MiniModel = _configService.GetModel("GPT-4o-Mini");
        WorkItemChat chat = new();
        chat.Settings = new ChatSettings()
        {
            MaxTokens = gpt4MiniModel.MaxTokens,
            Model = gpt4MiniModel.Name,
            Temperature = 0.5f
        };
        chat.Type = WorkItemType.Chat;
        chat.Settings.Prompt = $"Using the input that is a knowledge article, generate between {numToGenerateMin} and {numToGenerateMax} questions a person may ask that this article answers. Generate the questions in norwegian language. Give me the answer as json in the following format: {{ \"Questions\" : [ \"question1\", \"question2\" ] }}. Return the json string only no other information. Do not include ```json literal.";
        chat.Messages.Add(new ChatUiT2.Models.ChatMessage()
        {
            Role = ChatMessageRole.User,
            Content = content
        });
        var chatResponse = await GetTextResponseForChat(chat);
        return JsonSerializer.Deserialize<QuestionsFromTextResult>(chatResponse);
    }

    public async Task SaveRagProject(RagProject ragProject)
    {
        if(string.IsNullOrEmpty(ragProject.Configuration?.DbName))
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

    private async Task SaveRagProjectItem(ContentItem item, IMongoCollection<BsonDocument> itemCollection)
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
        if(string.IsNullOrEmpty(projectId))
        {
            throw new ArgumentException("projectId must be set to get project");
        }
        var filter = Builders<BsonDocument>.Filter.Eq("_id", projectId);
        var documents = await _ragProjectDefinitionsItemCollection.FindAsync(filter);
        var ragProject = BsonSerializer.Deserialize<RagProject>(documents.FirstOrDefault().AsBsonDocument);

        // Get the items for the project
        if(loadItems)
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

    public async Task DeleteOrphanEmbeddings()
    {
        var pipeline = new[]
        {
            new BsonDocument("$lookup", new BsonDocument
            {
                { "from", "TopdeskKnowledgeItemEmbeddings" },
                { "localField", "TopdeskKnowledgeItemId" },
                { "foreignField", "TopdeskId" },
                { "as", "children_docs" }
            }),
            new BsonDocument("$match", new BsonDocument
            {
                { "children_docs", new BsonDocument("$eq", new BsonArray()) }
            })
        };

        var orphans = _topdeskKnowledgeItemEmbeddingCollection.Aggregate<TopdeskTextEmbedding>(pipeline).ToList();
        foreach (var orphan in orphans)
        {
            await DeleteTopdeskEmbedding(orphan);
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
        await file.OpenReadStream().CopyToAsync(stream);
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
    /// Get embeddings for an item in a rag project db
    /// </summary>
    /// <param name="topdeskId">The knowledgeItem id in topdesk</param>
    /// <returns></returns>
    public async Task<List<RagTextEmbedding>> GetEmbeddingsByItemId(RagProject ragProject, string itemId)
    {
        if(string.IsNullOrEmpty(itemId))
        {
            throw new ArgumentException("itemId must be set to get embeddings");
        }
        List<RagTextEmbedding> result = [];
        var ragItemsDatabase = _mongoClientRagDb.GetDatabase(ragProject.Configuration.DbName);
        var itemCollection = ragItemsDatabase.GetCollection<BsonDocument>(ragProject.Configuration.ItemCollectionName);

        var filter = Builders<BsonDocument>.Filter.Eq("SourceItemId", itemId);
        var documents = await itemCollection.FindAsync(filter);

        foreach (var doc in documents.ToList())
        {
            var knowledgeItem = BsonSerializer.Deserialize<RagTextEmbedding>(doc.AsBsonDocument);
            result.Add(knowledgeItem);
        }

        return result;
    }


    /// <summary>
    /// Get all embeddings for a rag project db
    /// </summary>
    /// <param name="ragProject">The project to get for</param>
    /// <returns></returns>
    public async Task<List<RagTextEmbedding>> GetEmbeddingsByProject(RagProject ragProject)
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
}
