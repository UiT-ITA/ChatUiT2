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

namespace ChatUiT2.Services;

public class RagTopdeskDatabaseService : IRagTopdeskDatabaseService
{
    private readonly IConfiguration _configuration;

    // Services
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IConfigService _configService;

    // Collections
    private readonly IMongoCollection<BsonDocument> _topdeskKnowledgeItemCollection;
    private readonly IMongoCollection<BsonDocument> _topdeskKnowledgeItemEmbeddingCollection;

    public RagTopdeskDatabaseService(IConfiguration configuration,
                                     IDateTimeProvider dateTimeProvider,
                                     IConfigService configService)
    {
        this._configuration = configuration;
        this._dateTimeProvider = dateTimeProvider;
        this._configService = configService;
        var connectionString = configuration.GetConnectionString("MongoDbRagTopdesk");

        var client = new MongoClient(connectionString);

        var userDatabase = client.GetDatabase("RagTopdesk");

        _topdeskKnowledgeItemCollection = userDatabase.GetCollection<BsonDocument>("TopdeskKnowledgeItems");
        _topdeskKnowledgeItemEmbeddingCollection = userDatabase.GetCollection<BsonDocument>("TopdeskKnowledgeItemEmbeddings");
    }


    /// <summary>
    /// Get topdesk knowledgeItems from the database
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    public async Task<List<TopdeskKnowledgeItem>> GetAllTopdeskKnowledgeItems()
    {
        List<TopdeskKnowledgeItem> result = [];
        var documents = await _topdeskKnowledgeItemCollection.FindAsync(new BsonDocument());

        foreach (var doc in documents.ToList())
        {
            var knowledgeItem = BsonSerializer.Deserialize<TopdeskKnowledgeItem>(doc.AsBsonDocument);
            result.Add(knowledgeItem);
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
        await _topdeskKnowledgeItemEmbeddingCollection.UpdateManyAsync(FilterDefinition<BsonDocument>.Empty, update);
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
                    Source = RagSource.Topdesk,
                    Text = embedding.Originaltext,
                };
                result.Add(ragResult);
            }
        }
        result = result.Where(x => x.MatchScore >= minMatchScore).ToList();
        if(result.Count() >= numResults)
        {
            return result.OrderByDescending(x => x.MatchScore).Take(numResults).ToList();
        } else
        {
            return result.OrderByDescending(x => x.MatchScore).ToList();
        }
    }
}
