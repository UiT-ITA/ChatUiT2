using MongoDB.Bson.Serialization.Attributes;

namespace ChatUiT2.Models.RagProject;

/// <summary>
/// Model class for a participant in a rag project
/// </summary>
public class Participant
{
    [BsonElement("Displayname")]
    public string Displayname { get; set; } = string.Empty;
    [BsonElement("UitUsername")]
    public string UitUsername { get; set; } = string.Empty;
    [BsonElement("Role")]
    public string Role { get; set; } = string.Empty;
}
