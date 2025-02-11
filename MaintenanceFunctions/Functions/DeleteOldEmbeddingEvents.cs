using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using ChatUiT2_Classlib.Model.RabbitMq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using UiT.ChatUiT2.MaintenanceFunctions.Model;
using UiT.ChatUiT2.MaintenanceFunctions.Tools;
using UiT.CommonToolsLib.Services;

namespace UiT.ChatUiT2.MaintenanceFunctions.Functions;

/// <summary>
/// Function to delete old embedding events in the database
/// There may be events that never was processed and are now old
/// </summary>
public class DeleteOldEmbeddingEvents
{
    private readonly ILogger _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IRagDatabaseService _ragDatabaseService;

    public DeleteOldEmbeddingEvents(ILogger<DeleteOldEmbeddingEvents> logger,
                                    IDateTimeProvider dateTimeProvider,
                                    IRagDatabaseService ragDatabaseService)
    {
        _logger = logger;
        this._dateTimeProvider = dateTimeProvider;
        this._ragDatabaseService = ragDatabaseService;
    }

    [Function("DeleteOldEmbeddingEvents")]
    public async Task Run([TimerTrigger("0 5 * * * *", RunOnStartup = DebugTools.IsDebug)] TimerInfo myTimer)
    {
        var startTime = _dateTimeProvider.UtcNow;

        try
        {
            _logger.LogInformation("Starting function {functionName}. {logType} {debugRelevance}",
                                   nameof(DeleteOldEmbeddingEvents),
                                   LogType.FunctionProcessingStarted,
                                   1);
            var numDeleted = 0;
            var ragProjects = await _ragDatabaseService.GetAllRagProjects();
            foreach (var ragProject in ragProjects)
            {
                try
                {
                    var embeddingEvents = await _ragDatabaseService.GetExpiredEmbeddingEvents(ragProject, 7);
                    foreach (var embeddingEvent in embeddingEvents)
                    {
                        await _ragDatabaseService.DeleteEmbeddingEvent(ragProject, embeddingEvent);
                        numDeleted++;
                    }
                    _logger.LogInformation("{functionName} deleted {numDeleted} embeddingEvents not updated in seven days for project {ragprojectId}. {logType} {debugRelevance}",
                                           nameof(DeleteOldEmbeddingEvents),
                                           numDeleted,
                                           ragProject.Id,
                                           LogType.DeleteCount,
                                           1);
                }
                catch (Exception e)
                {
                    _logger.LogError("{functionName} error deleting old embedding events for project: {ragprojectId} {errorMessage} {functionName} {logType} {debugRelevance}",
                                     nameof(DeleteOldEmbeddingEvents),
                                     ragProject.Id,
                                     e.Message,
                                     nameof(DeleteOldEmbeddingEvents),
                                     LogType.FunctionError,
                                     3);

                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError("{functionName} error deleting old embedding events: {errorMessage} {functionName} {logType} {debugRelevance}",
                             nameof(DeleteOldEmbeddingEvents),
                             e.Message,
                             nameof(DeleteOldEmbeddingEvents),
                             LogType.FunctionError,
                             3);
        }
        finally
        {
            var endTime = _dateTimeProvider.UtcNow;
            var runTime = Math.Round((endTime - startTime).TotalMilliseconds);
            _logger.LogInformation("Delete old embedding events finished in {elapsedMs} ms. {logType} {functionName} {debugRelevance}",
                                   runTime,
                                   LogType.FunctionProcessingFinished,
                                   nameof(DeleteOldEmbeddingEvents),
                                   2);
        }
    }
}
