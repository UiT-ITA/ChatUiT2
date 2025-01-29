using UiT.ChatUiT2.MaintenanceFunctions.Model;

namespace ChatUiT2.Interfaces;
public interface IRabbitMqService
{
    public string GetRoutingKey(RagMqMessage message);
    public void SendRagMessage(RagMqMessage message);
}