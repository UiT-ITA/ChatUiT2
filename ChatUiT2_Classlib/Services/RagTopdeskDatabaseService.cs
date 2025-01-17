using ChatUiT2.Interfaces;
using ChatUiT2_Classlib.Model.Topdesk;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using UiT.CommonToolsLib.Services;
using UiT.RestClientTopdesk.Model;

namespace ChatUiT2.Services;

public class RagTopdeskDatabaseService : IRagTopdeskDatabaseService
{
    // Services
    private readonly IDateTimeProvider _dateTimeProvider;

    // Collections
    private readonly IMongoCollection<BsonDocument> _topdeskKnowledgeItemCollection;
    private readonly IMongoCollection<BsonDocument> _topdeskKnowledgeItemEmbeddingCollection;

    public RagTopdeskDatabaseService(IConfiguration configuration,
                           IDateTimeProvider dateTimeProvider)
    {
        this._dateTimeProvider = dateTimeProvider;
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
}
