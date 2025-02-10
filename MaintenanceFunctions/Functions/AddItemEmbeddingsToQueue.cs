using ChatUiT2.Interfaces;
using ChatUiT2_Classlib.Model.RabbitMq;
using ChatUiT2_Classlib.Model.RagProject;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using UiT.ChatUiT2.MaintenanceFunctions.Model;

namespace UiT.ChatUiT2.MaintenanceFunctions.Functions;

/// <summary>
/// Checks for content items in a rag project that are missing embeddings and adds 
/// them to the queue for processing.
/// This function handles tasks that are fast to process. Like adding messages to a queue
/// that handles the time consuming embedding generation.
/// It can also cancel all embeddings processing for a project.
/// </summary>
public class AddItemEmbeddingsToQueue
{
    private readonly ILogger _logger;
    private readonly IRagDatabaseService _ragTopdeskDatabaseService;
    private readonly IRabbitMqService _rabbitMqService;

    public AddItemEmbeddingsToQueue(ILoggerFactory loggerFactory,
                                 IRagDatabaseService ragTopdeskDatabaseService,
                                 IRabbitMqService rabbitMqService)
    {
        _logger = loggerFactory.CreateLogger<AddItemEmbeddingsToQueue>();
        this._ragTopdeskDatabaseService = ragTopdeskDatabaseService;
        this._rabbitMqService = rabbitMqService;
    }

    [Function("AddItemEmbeddingsToQueue")]
    public async Task Run([RabbitMQTrigger("rag-add-process-embedding-tasks", ConnectionStringSetting = "RabbitMqConnectionString")] RagMqMessage myQueueItem, FunctionContext context)
    {
        var operation = myQueueItem.Operation == null ? string.Empty : Enum.GetName(typeof(RagMqMessageOperations), myQueueItem.Operation!);
        _logger.LogInformation("Processing rag message {function} {ragProjectId} {itemMongoDbId} {operation}",
                               nameof(AddItemEmbeddingsToQueue),
                               myQueueItem.RagProjectId,
                               myQueueItem.SourceItemMongoDbId,
                               operation);
        try
        {
            var ragProject = await _ragTopdeskDatabaseService.GetRagProjectById(myQueueItem.RagProjectId);
            if (ragProject == null)
            {
                _logger.LogError("{functionName} error processing message for resource type {itemMongoDbId}, Rag project {ragProjectId} not found. operation {operation} ",
                                 nameof(AddItemEmbeddingsToQueue),
                                 myQueueItem.SourceItemMongoDbId,
                                 myQueueItem.RagProjectId,
                                 Enum.GetName(typeof(RagMqMessageOperations), myQueueItem.Operation));
                return;
            }
            switch(myQueueItem.Operation)
            {
                case RagMqMessageOperations.ScanForItemsMissingEmbeddings:
                    await AddItemsMissingEmbeddingsToQueue(ragProject, myQueueItem);
                    break;
                case RagMqMessageOperations.CancelAllEmbeddingsProcessing:
                    await CancelAllEmbeddingsProcessing(ragProject);
                    break;
                default:
                    _logger.LogWarning("{functionName} error processing message for resource type {itemMongoDbId} operation {operation}: Unknown operation.",
                    nameof(AddItemEmbeddingsToQueue),
                    myQueueItem.SourceItemMongoDbId,
                    operation);
                    break;
            }
        }
        catch (Exception e)
        {
            _logger.LogError("{functionName} error processing message for resource type {itemMongoDbId} operation {operation} message {errorMessage} stackTrace {stackTrace} innerMessage {innerMessage} innerStackTrace {innerStackTrace}.",
                             nameof(AddItemEmbeddingsToQueue),
                             myQueueItem.SourceItemMongoDbId,
                             operation,
                             e.Message,
                             e.StackTrace,
                             e.InnerException?.Message,
                             e.InnerException?.StackTrace);
        } finally
        {
            _logger.LogInformation("Processing rag message completed {function} {ragProjectId} {itemMongoDbId} {operation}",
                                   nameof(AddItemEmbeddingsToQueue),
                                   myQueueItem.RagProjectId,
                                   myQueueItem.SourceItemMongoDbId,
                                   operation);
        }
    }
    private async Task AddItemsMissingEmbeddingsToQueue(RagProject ragProject, RagMqMessage myQueueItem)
    {
        if(string.IsNullOrEmpty(ragProject.Id))
        {
            _logger.LogWarning("AddItemsMissingEmbeddingsToQueue: Missing rag project id or source item id");
            return;
        }
        var itemsMissingEmbeddings = await _ragTopdeskDatabaseService.GetContentItemsWithNoEmbeddings(ragProject);        
        foreach (var item in itemsMissingEmbeddings)
        {
            if(string.IsNullOrEmpty(item.Id))
            {
                _logger.LogWarning("{functionName}.AddItemsMissingEmbeddingsToQueue: Missing item id",
                                   nameof(AddItemEmbeddingsToQueue));
                continue;
            }
            try
            {
                foreach (var embeddingType in ragProject.Configuration.EmbeddingTypes)
                {
                    if (await _ragTopdeskDatabaseService.EmbeddingEventExists(ragProject, item.Id, embeddingType))
                    {
                        _logger.LogWarning("{functionName}.AddItemsMissingEmbeddingsToQueue: Item {itemId} with embeddingType {embeddingType} already exist in collection",
                                           nameof(AddItemEmbeddingsToQueue),
                                           item.Id,
                                           embeddingType);
                        continue;
                    }

                    EmbeddingEvent embeddingEvent = new EmbeddingEvent
                    {
                        RagProjectId = ragProject.Id,
                        ContentItemId = item.Id,
                        EventType = EmbeddingEventType.Create,
                        Created = DateTimeOffset.UtcNow,
                        Updated = DateTimeOffset.UtcNow,
                        IsProcessing = false,
                        IsCompleted = false
                    };
                    await _ragTopdeskDatabaseService.SaveRagEmbeddingEvent(ragProject, embeddingEvent);

                    RagMqMessage message = new RagMqMessage
                    {
                        RagProjectId = ragProject.Id,
                        SourceItemMongoDbId = item.Id,
                        Operation = RagMqMessageOperations.GenerateEmbeddings,
                        EmbeddingEventMongoDbId = embeddingEvent.Id ?? string.Empty
                    };
                    await _rabbitMqService.SendRagMessage(message);
                }
                await _ragTopdeskDatabaseService.SaveRagProjectItem(ragProject, item);
            }
            catch (Exception e)
            {
                _logger.LogError("{functionName}.AddItemsMissingEmbeddingsToQueue: Error sending message for item {itemId} error {errorMessage} stackTrace {stackTrace} innerMessage {innerMessage} innerStackTrace {innerStackTrace}",
                                 nameof(AddItemEmbeddingsToQueue),
                                 item.Id,
                                 e.Message,
                                 e.StackTrace,
                                 e.InnerException?.Message,
                                 e.InnerException?.StackTrace);
            }
        }
    }

    private async Task CancelAllEmbeddingsProcessing(RagProject ragProject)
    {
        if (string.IsNullOrEmpty(ragProject.Id))
        {
            _logger.LogWarning("CancelAllEmbeddingsProcessing: Missing rag project id or source item id");
            return;
        }
        await _ragTopdeskDatabaseService.CancelAllEmbeddingProcessing(ragProject);
    }
}
