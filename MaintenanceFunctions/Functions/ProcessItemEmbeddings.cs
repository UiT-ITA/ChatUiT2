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
    private readonly IRagDatabaseService _ragTopdeskDatabaseService;
    private readonly IRabbitMqService _rabbitMqService;

    public ProcessItemEmbeddings(ILoggerFactory loggerFactory,
                                 IRagDatabaseService ragTopdeskDatabaseService,
                                 IRabbitMqService rabbitMqService)
    {
        _logger = loggerFactory.CreateLogger<ProcessItemEmbeddings>();
        this._ragTopdeskDatabaseService = ragTopdeskDatabaseService;
        this._rabbitMqService = rabbitMqService;
    }

    [Function("ProcessItemEmbeddings")]
    public async Task Run([RabbitMQTrigger("rag-process-embedding", ConnectionStringSetting = "RabbitMqConnectionString")] RagMqMessage myQueueItem, FunctionContext context)
    {
        var operation = myQueueItem.Operation == null ? string.Empty : Enum.GetName(typeof(RagMqMessageOperations), myQueueItem.Operation!);

        _logger.LogInformation("Processing rag message {function} {ragProjectId} {itemMongoDbId} {operation}",
                               myQueueItem.RagProjectId,
                               nameof(ProcessItemEmbeddings),
                               myQueueItem.SourceItemMongoDbId,
                               operation);
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
                case RagMqMessageOperations.GenerateEmbeddings:
                    await CreateItemEmbeddings(ragProject, myQueueItem);
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
        var embeddingEvent = await _ragTopdeskDatabaseService.GetEmbeddingEventByIdForProcessing(ragProject, myQueueItem.EmbeddingEventMongoDbId);
        if (embeddingEvent == null)
        {
            _logger.LogWarning("{functionName} error processing message for resource type {itemMongoDbId} operation {operation}: Embedding event not found or is being updated by someone else.",
                               nameof(ProcessItemEmbeddings),
                               myQueueItem.SourceItemMongoDbId,
                               Enum.GetName(typeof(RagMqMessageOperations), myQueueItem.Operation));
            return;
        }
        switch (embeddingEvent.EmbeddingSourceType)
        {
            case EmbeddingSourceType.Question:
                await CreateQuestionItemEmbeddings(ragProject, myQueueItem);
                break;
            case EmbeddingSourceType.Paragraph:
                await CreateParagraphItemEmbeddings(ragProject, myQueueItem);
                break;
            default:
                _logger.LogWarning("{functionName} error processing message for resource type {itemMongoDbId} operation {operation}: Unknown embedding type: {embeddingType}.",
                                   nameof(ProcessItemEmbeddings),
                                   myQueueItem.SourceItemMongoDbId,
                                   Enum.GetName(typeof(RagMqMessageOperations), myQueueItem.Operation),
                                   Enum.GetName(typeof(EmbeddingSourceType), embeddingEvent.EmbeddingSourceType));
                break;
        }
    }
    private async Task CreateQuestionItemEmbeddings(RagProject ragProject, RagMqMessage myQueueItem)
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
        await _ragTopdeskDatabaseService.GenerateRagQuestionsFromContent(ragProject, sourceItem);
        await _ragTopdeskDatabaseService.DeleteEmbeddingEvent(ragProject, myQueueItem.EmbeddingEventMongoDbId);
    }

    private async Task CreateParagraphItemEmbeddings(RagProject ragProject, RagMqMessage myQueueItem)
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
        await _ragTopdeskDatabaseService.GenerateRagParagraphsFromContent(ragProject, sourceItem);
        await _ragTopdeskDatabaseService.DeleteEmbeddingEvent(ragProject, myQueueItem.EmbeddingEventMongoDbId);
    }
}
