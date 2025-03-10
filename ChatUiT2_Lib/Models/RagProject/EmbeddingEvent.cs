using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using Newtonsoft.Json;

namespace ChatUiT2.Models.RagProject;

/// <summary>
/// Class for signaling create/delete/update of embedding
/// Can be stored in mongodb collection alongside a RagProjects other resources
/// </summary>
public class EmbeddingEvent
{
    /// <summary>
    /// MongoDB Id
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonProperty(PropertyName = "id")]
    public string? Id { get; set; }
    [BsonElement("RagProjectId")]
    public string RagProjectId { get; set; } = string.Empty;
    [BsonElement("ContentItemId")]
    public string ContentItemId { get; set; } = string.Empty;
    [BsonElement("EventType")]
    public EmbeddingEventType EventType { get; set; }
    [BsonElement("Created")]
    public DateTimeOffset Created { get; set; } = DateTimeOffset.MinValue;
    [BsonElement("Updated")]
    public DateTimeOffset Updated { get; set; } = DateTimeOffset.MinValue;
    [BsonElement("IsProcessing")]
    public bool IsProcessing { get; set; }
    [BsonElement("EmbeddingSourceType")]
    public EmbeddingSourceType EmbeddingSourceType { get; set; }
}

public enum EmbeddingEventType
{
    // Creates a new embedding if no embeddings exist on item
    Create = 1,
    // Recreates embeddings on item, if embeddings exist they are deleted and new ones are created
    Recreate = 2,
    // Deletes embeddings for an item
    Delete = 3
}
