using ChatUiT2.Models.RabbitMq;

namespace ChatUiT2.Interfaces;
public interface IRabbitMqService
{
    Task<IEnumerable<T>> GetAllMessagesInQueueWithoutRemoval<T>(string queueName);
    Task<uint> GetQueueCount(string queueName);
    public string GetRoutingKey(RagMqMessage message);
    public Task SendRagMessage(RagMqMessage message);
    Task SendRagMessages(IEnumerable<RagMqMessage> messages);
}