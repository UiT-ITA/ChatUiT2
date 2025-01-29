using ChatUiT2.Interfaces;
using ChatUiT2_Classlib.Model.RabbitMq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using UiT.ChatUiT2.MaintenanceFunctions.Model;

namespace UiT.ChatUiT2.MaintenanceFunctions.Functions;

public class ProcessItemEmbeddings
{
    private readonly ILogger _logger;
    private readonly IRagTopdeskDatabaseService _ragTopdeskDatabaseService;

    public ProcessItemEmbeddings(ILoggerFactory loggerFactory,
                                 IRagTopdeskDatabaseService ragTopdeskDatabaseService)
    {
        _logger = loggerFactory.CreateLogger<ProcessItemEmbeddings>();
        this._ragTopdeskDatabaseService = ragTopdeskDatabaseService;
    }

    [Function("ProcessItemEmbeddings")]
    public async Task Run([RabbitMQTrigger("rag-tasks", ConnectionStringSetting = "RabbitMqConnectionString")] RagMqMessage myQueueItem, FunctionContext context)
    {
        _logger.LogInformation("Processing rag message {function} {itemMongoDbId}",
                               nameof(ProcessItemEmbeddings),
                               myQueueItem.SourceItemMongoDbId);
        try
        {
            var ragProject = await _ragTopdeskDatabaseService.GetRagProjectById(myQueueItem.RagProjectId);
            if (ragProject == null)
            {
                _logger.LogError("{functionName} error processing message for resource type {itemMongoDbId}, Rag project {ragProjectId} not found. operation {operation} ",
                                 nameof(ProcessItemEmbeddings),
                                 myQueueItem.SourceItemMongoDbId,
                                 myQueueItem.RagProjectId,
                                 Enum.GetName(typeof(RagMqMessageOperations), myQueueItem.Operation));
                return;
            }
            var sourceItem = await _ragTopdeskDatabaseService.GetContentItemById(ragProject, myQueueItem.SourceItemMongoDbId);
            if (sourceItem == null)
            {
                _logger.LogError("{functionName} error processing message for resource type {itemMongoDbId}, Source item not found. Rag project {ragProjectId}. operation {operation} ",
                                 nameof(ProcessItemEmbeddings),
                                 myQueueItem.SourceItemMongoDbId,
                                 myQueueItem.RagProjectId,
                                 Enum.GetName(typeof(RagMqMessageOperations), myQueueItem.Operation));
                return;
            }
            await _ragTopdeskDatabaseService.GenerateRagQuestionsFromContent(ragProject, sourceItem);
        }
        catch (Exception e)
        {
            _logger.LogError("{functionName} error processing message for resource type {itemMongoDbId} operation {operation} message {errorMessage} stackTrace {stackTrace} innerMessage {innerMessage} innerStackTrace {innerStackTrace}.",
                             nameof(ProcessItemEmbeddings),
                             myQueueItem.SourceItemMongoDbId,
                             Enum.GetName(typeof(RagMqMessageOperations), myQueueItem.Operation),
                             e.Message,
                             e.StackTrace,
                             e.InnerException?.Message,
                             e.InnerException?.StackTrace);
        }
    }
}
