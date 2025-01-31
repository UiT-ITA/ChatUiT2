using ChatUiT2.Interfaces;
using ChatUiT2_Classlib.Model.RabbitMq;
using ChatUiT2_Classlib.Model.RagProject;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using UiT.ChatUiT2.MaintenanceFunctions.Model;

namespace UiT.ChatUiT2.MaintenanceFunctions.Functions;

/// <summary>
/// Handles rag messages in rabbit mq message queue
/// Some of the embedding tasks are long running and are handled by this function
/// By handling one message at a time we spread the load over time and do not risk
/// running into the 5 minute timeout of azure functions
/// Types of messages handled:
/// Message to calulate embeddings for a content item
/// Message to find all items missing embeddings and add them to the queue
/// </summary>
public class ProcessItemEmbeddings
{
    private readonly ILogger _logger;
    private readonly IRagTopdeskDatabaseService _ragTopdeskDatabaseService;
    private readonly IRabbitMqService _rabbitMqService;

    public ProcessItemEmbeddings(ILoggerFactory loggerFactory,
                                 IRagTopdeskDatabaseService ragTopdeskDatabaseService,
                                 IRabbitMqService rabbitMqService)
    {
        _logger = loggerFactory.CreateLogger<ProcessItemEmbeddings>();
        this._ragTopdeskDatabaseService = ragTopdeskDatabaseService;
        this._rabbitMqService = rabbitMqService;
    }

    [Function("ProcessItemEmbeddings")]
    public async Task Run([RabbitMQTrigger("rag-tasks", ConnectionStringSetting = "RabbitMqConnectionString")] RagMqMessage myQueueItem, FunctionContext context)
    {
        _logger.LogInformation("Processing rag message {function} {ragProjectId} {itemMongoDbId} {operation}",
                               myQueueItem.RagProjectId,
                               nameof(ProcessItemEmbeddings),
                               myQueueItem.SourceItemMongoDbId,
                               myQueueItem.Operation);
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
            switch(myQueueItem.Operation)
            {
                case RagMqMessageOperations.GenerateQuestionEmbeddings:
                    await CreateItemEmbeddings(ragProject, myQueueItem);
                    break;
                case RagMqMessageOperations.ScanForItemsMissingEmbeddings:
                    await AddItemsMissingEmbeddingsToQueue(ragProject);
                    break;
                case RagMqMessageOperations.CancelAllEmbeddingsProcessing:
                    await CancelAllEmbeddingsProcessing(ragProject);
                    break;
                default:
                    _logger.LogWarning("{functionName} error processing message for resource type {itemMongoDbId} operation {operation}: Unknown operation.",
                    nameof(ProcessItemEmbeddings),
                    myQueueItem.SourceItemMongoDbId,
                    Enum.GetName(typeof(RagMqMessageOperations), myQueueItem.Operation));
                    break;
            }
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

    private async Task CreateItemEmbeddings(RagProject ragProject, RagMqMessage myQueueItem)
    {
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
        if (sourceItem.EmbeddingsCreationInProgress == false)
        {
            _logger.LogWarning("{functionName} error processing message for resource type {itemMongoDbId}, embeddings creation flag is false, maybe old message that is already processed. Rag project {ragProjectId}. operation {operation} ",
                             nameof(ProcessItemEmbeddings),
                             myQueueItem.SourceItemMongoDbId,
                             myQueueItem.RagProjectId,
                             Enum.GetName(typeof(RagMqMessageOperations), myQueueItem.Operation));
            return;
        }
        await _ragTopdeskDatabaseService.GenerateRagQuestionsFromContent(ragProject, sourceItem);
        sourceItem.EmbeddingsCreationInProgress = false;
        await _ragTopdeskDatabaseService.SaveRagProjectItem(ragProject, sourceItem);
    }

    private async Task AddItemsMissingEmbeddingsToQueue(RagProject ragProject)
    {
        if(string.IsNullOrEmpty(ragProject.Id))
        {
            _logger.LogWarning("AddItemsMissingEmbeddingsToQueue: Missing rag project id or source item id");
            return;
        }
        var itemsMissingEmbeddings = await _ragTopdeskDatabaseService.GetContentItemsWithNoEmbeddings(ragProject);
        itemsMissingEmbeddings = itemsMissingEmbeddings.Take(3).ToList();
        foreach (var item in itemsMissingEmbeddings)
        {
            if(string.IsNullOrEmpty(item.Id))
            {
                _logger.LogWarning("{functionName}.AddItemsMissingEmbeddingsToQueue: Missing item id",
                                   nameof(ProcessItemEmbeddings));
                continue;
            }
            if (item.EmbeddingsCreationInProgress)
            {
                _logger.LogWarning("{functionName}.AddItemsMissingEmbeddingsToQueue: Item {itemId} embeddings creation already in progress",
                                   nameof(ProcessItemEmbeddings),
                                   item.Id);
                continue;
            }
            try
            {
                RagMqMessage message = new RagMqMessage
                {
                    RagProjectId = ragProject.Id,
                    SourceItemMongoDbId = item.Id,
                    Operation = RagMqMessageOperations.GenerateQuestionEmbeddings
                };
                _rabbitMqService.SendRagMessage(message);
                item.EmbeddingsCreationInProgress = true;
                await _ragTopdeskDatabaseService.SaveRagProjectItem(ragProject, item);
            }
            catch (Exception e)
            {
                _logger.LogError("{functionName}.AddItemsMissingEmbeddingsToQueue: Error sending message for item {itemId} error {errorMessage} stackTrace {stackTrace} innerMessage {innerMessage} innerStackTrace {innerStackTrace}",
                                 nameof(ProcessItemEmbeddings),
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
