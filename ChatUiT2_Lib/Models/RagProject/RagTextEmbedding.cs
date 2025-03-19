using Microsoft.Extensions.AI;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using OpenAI.Embeddings;
using Newtonsoft.Json;

namespace ChatUiT2.Models.RagProject;

public class RagTextEmbedding
{
    /// <summary>
    /// MongoDB Id
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonProperty(PropertyName = "id")]
    public string Id { get; set; } = string.Empty;
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
    /// The source item this embedding belongs to
    /// </summary>
    [BsonElement("SourceItemId")]
    public string SourceItemId { get; set; } = string.Empty;
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
    [BsonElement("RagProjectId")]
    public string RagProjectId { get; set; } = string.Empty;

    [BsonElement("EmbeddingSourceType")]
    public EmbeddingSourceType? TextType { get; set; }    

    [BsonIgnore]
    public ContentItem? ContentItem { get; set; }
}
