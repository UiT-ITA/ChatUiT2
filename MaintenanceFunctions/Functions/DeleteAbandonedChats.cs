using ChatUiT2.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using UiT.ChatUiT2.MaintenanceFunctions.Model;
using UiT.ChatUiT2.MaintenanceFunctions.Tools;
using UiT.CommonToolsLib.Services;

namespace UiT.ChatUiT2.MaintenanceFunctions.Functions
{
    public class DeleteAbandonedChats
    {
        private readonly ILogger _logger;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IDatabaseService _databaseService;

        public DeleteAbandonedChats(ILogger<DeleteAbandonedChats> logger,
                                    IDateTimeProvider dateTimeProvider,
                                    IDatabaseService databaseService)
        {
            _logger = logger;
            this._dateTimeProvider = dateTimeProvider;
            this._databaseService = databaseService;
        }

        [Function("Function1")]
        public async Task Run([TimerTrigger("0 */5 * * * *", RunOnStartup = DebugTools.IsDebug)] TimerInfo myTimer)
        {
            var startTime = _dateTimeProvider.UtcNow;

            try
            {
                _logger.LogInformation("Starting function {functionName}. {logType} {debugRelevance}",
                                       nameof(DeleteAbandonedChats),
                                       LogType.FunctionProcessingStarted,
                                       1);

                var expiredWorkItems = await _databaseService.GetWorkItemsExpired();

                /*
                _logger.LogInformation("{functionName} deleted {numDeleted} jobRunLogs. {logType} {debugRelevance}",
                                       nameof(DeleteAbandonedChats),
                                       numDeleted,
                                       LogType.DeleteCount,
                                       2);
                */
            }
            catch (Exception e)
            {
                _logger.LogError("Error deleting old chats: {errorMessage} {functionName} {logType} {debugRelevance}",
                                 e.Message,
                                 nameof(DeleteAbandonedChats),
                                 LogType.FunctionError,
                                 3);
            }
            finally
            {
                var endTime = _dateTimeProvider.UtcNow;
                var runTime = Math.Round((endTime - startTime).TotalMilliseconds);
                _logger.LogInformation("Delete old chats finished in {elapsedMs} ms. {logType} {functionName} {debugRelevance}",
                                       runTime,
                                       LogType.FunctionProcessingFinished,
                                       nameof(DeleteAbandonedChats),
                                       2);
            }
        }
    }
}
