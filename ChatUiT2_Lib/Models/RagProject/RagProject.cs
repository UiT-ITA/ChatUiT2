using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using Newtonsoft.Json;

namespace ChatUiT2.Models.RagProject;

/// <summary>
/// Model class for a generic rag project
/// </summary>
[BsonIgnoreExtraElements]
public class RagProject
{
    /// <summary>
    /// Id
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonProperty(PropertyName = "id")]
    public string? Id { get; set; }
    [BsonElement("Name")]
    public string Name { get; set; } = string.Empty;
    [BsonElement("Description")]
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// Which departments have requested this project
    /// </summary>
    [BsonElement("RequestorDepartments")]
    public List<Department> RequestorDepartments { get; set; } = [];
    /// <summary>
    /// Who are the participants in this project
    /// </summary>
    [BsonElement("Participants")]
    public List<Participant> Participants { get; set; } = [];
    /// <summary>
    /// Configuration for the project
    /// </summary>
    [BsonElement("Configuration")]
    public RagConfiguration? Configuration { get; set; }
    [BsonIgnore]
    [JsonIgnore]
    public List<ContentItem> ContentItems { get; set; } = [];    
    [BsonElement("Created")]
    public DateTimeOffset Created { get; set; } = DateTimeOffset.MinValue;
    [BsonElement("Updated")]
    public DateTimeOffset Updated { get; set; } = DateTimeOffset.MinValue;
}
