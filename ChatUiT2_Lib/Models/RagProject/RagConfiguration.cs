using MongoDB.Bson.Serialization.Attributes;

namespace ChatUiT2.Models.RagProject;

/// <summary>
/// Configuration for a RAG project
/// </summary>
public class RagConfiguration
{
    [BsonElement("MinNumberOfQuestionsPerItem")]
    public int MinNumberOfQuestionsPerItem { get; set; } = 5;
    [BsonElement("MaxNumberOfQuestionsPerItem")]
    public int MaxNumberOfQuestionsPerItem { get; set; } = 20;
    [BsonElement("ModelForQuestionGeneration")]
    public string ModelForQuestionGeneration { get; set; } = string.Empty;
    [BsonElement("ModelForEmbeddings")]
    public string ModelForEmbeddings { get; set; } = string.Empty;
    [BsonElement("DbName")]
    public string DbName { get; set; } = string.Empty;
    [BsonElement("ItemCollectionName")]
    public string ItemCollectionName { get; set; } = string.Empty;
    [BsonElement("EmbeddingCollectioName")]
    public string EmbeddingCollectioName { get; set; } = string.Empty;
    [BsonElement("EmbeddingTypes")]
    public List<EmbeddingSourceType> EmbeddingTypes { get; set; } = [];
    [BsonElement("EmbeddingEventCollectioName")]
    public string EmbeddingEventCollectioName { get; set; } = string.Empty;
}
