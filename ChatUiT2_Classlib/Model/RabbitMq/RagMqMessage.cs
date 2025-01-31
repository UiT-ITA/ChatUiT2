namespace ChatUiT2_Classlib.Model.RabbitMq;

public class RagMqMessage
{
    public string RagProjectId { get; set; } = string.Empty;
    public string SourceItemMongoDbId { get; set; } = string.Empty;
    public DateTimeOffset MessageSentTime { get; set; } = DateTimeOffset.UtcNow;
    public RagMqMessageOperations Operation { get; set; } = RagMqMessageOperations.GenerateQuestionEmbeddings;
}
