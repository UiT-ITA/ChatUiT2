using ChatUiT2.Interfaces;
using ChatUiT2_Classlib.Model.Topdesk;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using UiT.CommonToolsLib.Services;

namespace ChatUiT2.Services;

public class RagTopdeskDatabaseService : IRagTopdeskDatabaseService
{
    // Services
    private readonly IDateTimeProvider _dateTimeProvider;

    // Collections
    private readonly IMongoCollection<BsonDocument> _topdeskArticleCollection;

    public RagTopdeskDatabaseService(IConfiguration configuration,
                           IDateTimeProvider dateTimeProvider)
    {
        this._dateTimeProvider = dateTimeProvider;
        var connectionString = configuration.GetConnectionString("MongoDbRagTopdesk");

        var client = new MongoClient(connectionString);

        var userDatabase = client.GetDatabase("RagTopdesk");

        _topdeskArticleCollection = userDatabase.GetCollection<BsonDocument>("TopdeskArticles");
    }


    /// <summary>
    /// Get topdesk articles from the database
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    public async Task<List<TopdeskArticle>> GetAllTopdeskArticles()
    {
        List<TopdeskArticle> result = [];
        var documents = await _topdeskArticleCollection.FindAsync(new BsonDocument());

        foreach (var doc in documents.ToList())
        {
            var article = BsonSerializer.Deserialize<TopdeskArticle>(doc.AsBsonDocument);
            result.Add(article);
        }

        return result;
    }

    public async Task SaveTopdeskArticle(TopdeskArticle topdeskArticle)
    {
        if (string.IsNullOrEmpty(topdeskArticle.Id))
        {
            topdeskArticle.Created = _dateTimeProvider.OffsetUtcNow;
            topdeskArticle.Updated = _dateTimeProvider.OffsetUtcNow;
            var document = topdeskArticle.ToBsonDocument();
            // This is new document, generate new id
            document["_id"] = ObjectId.GenerateNewId().ToString();
            await _topdeskArticleCollection.InsertOneAsync(document);
        }
        else
        {
            topdeskArticle.Updated = _dateTimeProvider.OffsetUtcNow;
            var document = topdeskArticle.ToBsonDocument();
            // This is an existing document, do update
            var filter = Builders<BsonDocument>.Filter.Eq("_id", topdeskArticle.Id);
            document.Remove("_id");
            await _topdeskArticleCollection.ReplaceOneAsync(filter, document);
        }
    }

}
