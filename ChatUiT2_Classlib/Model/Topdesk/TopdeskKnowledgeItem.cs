using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace ChatUiT2_Classlib.Model.Topdesk;

public class TopdeskKnowledgeItem
{
    /// <summary>
    /// MongoDB Id
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]

    public string? Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string TopdeskId { get; set; } = string.Empty;
    public DateTimeOffset Created { get; set; } = DateTimeOffset.MinValue;
    public DateTimeOffset Updated { get; set; } = DateTimeOffset.MinValue;

    [BsonIgnore]
    public List<TopdeskTextEmbedding> Embeddings { get; set; } = [];

}
