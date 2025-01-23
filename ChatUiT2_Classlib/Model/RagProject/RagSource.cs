using MongoDB.Bson.Serialization.Attributes;

namespace ChatUiT2_Classlib.Model.RagProject;

/// <summary>
/// Describes a source of content in a RAG project
/// </summary>
public class RagSource
{
    [BsonElement("SystemName")]
    public string SystemName { get; set; } = string.Empty;
    [BsonElement("ContentItems")]
    public List<Contentitem> ContentItems { get; set; } = [];
}
