using ChatUiT2.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using UiT.ChatUiT2.MaintenanceFunctions.Model;
using ChatUiT2_Classlib.Model.RabbitMq;

namespace UiT.ChatUiT2.MaintenanceFunctions.Functions;

/// <summary>
/// Use this class when debugging to send a test message to the RAG
/// rabbitMq queue.
/// </summary>
public class SendRagTestMessage
{
    private readonly ILogger<SendRagTestMessage> _logger;
    private readonly IRabbitMqService _rabbitMqService;

    public SendRagTestMessage(ILogger<SendRagTestMessage> logger,
                              IRabbitMqService rabbitMqService)
    {
        _logger = logger;
        this._rabbitMqService = rabbitMqService;
    }

    [Function("SendRagTestMessage")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        RagMqMessage message = new()
        {
            Operation = RagMqMessageOperations.ScanForItemsMissingEmbeddings,
            RagProjectId = "679379f33f0858dbff7b58d4",
            SourceItemMongoDbId = string.Empty
        };
        await _rabbitMqService.SendRagMessage(message);
        return new OkObjectResult("Welcome to Azure Functions!");
    }
}
