using ChatUiT2_Classlib.Model.RabbitMq;

namespace ChatUiT2.Interfaces;
public interface IRabbitMqService
{
    public string GetRoutingKey(RagMqMessage message);
    public void SendRagMessage(RagMqMessage message);
}