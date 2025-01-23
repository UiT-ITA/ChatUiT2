using MongoDB.Bson.Serialization.Attributes;

namespace ChatUiT2_Classlib.Model.RagProject;

/// <summary>
/// Model class for a department in a rag project
/// </summary>
public class Department
{
    [BsonElement("Name")]
    public string Name { get; set; } = string.Empty;
    [BsonElement("Stedkode")]
    public string Stedkode { get; set; } = string.Empty;
}
