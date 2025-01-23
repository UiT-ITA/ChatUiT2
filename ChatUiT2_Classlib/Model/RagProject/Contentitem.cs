using MongoDB.Bson.Serialization.Attributes;

namespace ChatUiT2_Classlib.Model.RagProject;

/// <summary>
/// Describes an item of content in a RAG project
/// </summary>
public class Contentitem
{
    [BsonElement("Type")]
    public string Type { get; set; } = string.Empty;
    [BsonElement("Title")]
    public string Title { get; set; } = string.Empty;
    [BsonElement("Description")]
    public string Description { get; set; } = string.Empty;
    [BsonElement("Content")]
    public string Content { get; set; } = string.Empty;
    [BsonElement("ContentUrl")]
    public string ContentUrl { get; set; } = string.Empty;
    [BsonElement("Language")]
    public string Language { get; set; } = string.Empty;
    [BsonElement("SourceSystemId")]
    public string SourceSystemId { get; set; } = string.Empty;
    [BsonElement("SourceSystemAltId")]
    public string SourceSystemAltId { get; set; } = string.Empty;    
}
