using ChatUiT2.Models.RagProject;

namespace ChatUiT2.Models.RabbitMq;

public class RagMqMessage
{
    public string RagProjectId { get; set; } = string.Empty;
    public string SourceItemMongoDbId { get; set; } = string.Empty;
    public string EmbeddingEventMongoDbId { get; set; } = string.Empty;
    public DateTimeOffset MessageSentTime { get; set; } = DateTimeOffset.UtcNow;
    public RagMqMessageOperations? Operation { get; set; }
    public EmbeddingSourceType? EmbeddingType { get; set; }
}
