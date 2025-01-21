using Microsoft.Extensions.AI;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using OpenAI.Embeddings;

namespace ChatUiT2_Classlib.Model.Topdesk;

public class TopdeskTextEmbedding
{
    /// <summary>
    /// MongoDB Id
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    /// <summary>
    /// The actual embedding
    /// </summary>
    [BsonElement("Embedding")]
    public float[]? Embedding { get; set; }
    /// <summary>
    /// The text this embedding was created from
    /// </summary>
    [BsonElement("OriginalText")]
    public string Originaltext { get; set; } = string.Empty;
    /// <summary>
    /// The knowledgeItem this embedding belongs to
    /// </summary>
    [BsonElement("TopdeskKnowledgeItemId")]
    public string TopdeskKnowledgeItemId { get; set; } = string.Empty;
    /// <summary>
    /// The name of the model used to create this embedding
    /// Example llama3.2
    /// </summary>
    [BsonElement("Model")]
    public string Model { get; set; } = string.Empty;
    /// <summary>
    /// Who provided the model
    /// Examples: ollama, openai etc
    /// </summary>
    [BsonElement("ModelProvider")]
    public string ModelProvider { get; set; } = string.Empty;
    [BsonElement("Created")]
    public DateTimeOffset Created { get; set; } = DateTimeOffset.MinValue;
    [BsonElement("Updated")]
    public DateTimeOffset Updated { get; set; } = DateTimeOffset.MinValue;

    [BsonIgnore]
    public TopdeskKnowledgeItem? TopdeskKnowledgeItem { get; set; }
}
