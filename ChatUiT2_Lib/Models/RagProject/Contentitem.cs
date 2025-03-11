using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace ChatUiT2.Models.RagProject;

/// <summary>
/// Describes an item of content in a RAG project
/// This is an article or a document or piece of text
/// </summary>
[BsonIgnoreExtraElements]
public class ContentItem
{
    /// <summary>
    /// MongoDB Id
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonProperty(PropertyName = "id")]
    public string? Id { get; set; }
    [BsonElement("SystemName")]
    public string SystemName { get; set; } = string.Empty;
    [BsonElement("DataType")]
    public string DataType { get; set; } = string.Empty;
    [BsonElement("ContentType")]
    public string ContentType { get; set; } = string.Empty;
    [BsonElement("Title")]
    public string Title { get; set; } = string.Empty;
    [BsonElement("Description")]
    public string Description { get; set; } = string.Empty;
    [BsonElement("ContentText")]
    public string ContentText { get; set; } = string.Empty;
    [BsonElement("ContentUrl")]
    public string ContentUrl { get; set; } = string.Empty;
    [BsonElement("ViewUrl")]
    public string ViewUrl { get; set; } = string.Empty;
    [BsonElement("Language")]
    public string Language { get; set; } = string.Empty;
    [BsonElement("SourceSystemId")]
    public string SourceSystemId { get; set; } = string.Empty;
    [BsonElement("SourceSystemAltId")]
    public string SourceSystemAltId { get; set; } = string.Empty;
    [BsonElement("Created")]
    public DateTimeOffset Created { get; set; } = DateTimeOffset.MinValue;
    [BsonElement("Updated")]
    public DateTimeOffset Updated { get; set; } = DateTimeOffset.MinValue;
    /// <summary>
    /// Added when storing to RAG database
    /// </summary>
    [BsonElement("RagProjectId")]
    public string RagProjectId { get; set; } = string.Empty;    
}
