
using ChatUiT2.Interfaces;
using ChatUiT2.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChatUiT2_Lib.Services;

/// <summary>
/// Use for copy rag database azure function.
/// It will need to have two instances pointing to different databases.
/// For ordinary webapp define service singleton directly in Program.cs
/// </summary>
public class RagDatabaseServiceFactory
{
    public static readonly string MainRagDatabase = "MainRagDatabase";
    public static readonly string CopyToRagDatabase = "CopyToRagDatabase";

    private readonly IDictionary<string, IRagDatabaseService> _clients;
    private readonly IChatToolsService _chatToolsService;

    public RagDatabaseServiceFactory(IServiceProvider sp,
                                     IChatToolsService chatToolsService)
    {
        _clients = new Dictionary<string, IRagDatabaseService>();
        this._chatToolsService = chatToolsService;
        var config = sp.GetRequiredService<IConfiguration>();
        var dateTimeProvider = sp.GetRequiredService<IDateTimeProvider>();
        var settingsService = sp.GetRequiredService<ISettingsService>();
        var memCache = sp.GetRequiredService<IMemoryCache>();
        var logger = sp.GetRequiredService<ILogger<RagDatabaseServiceCosmosDbNoSql>>();

        // Main service
        string connectionString = config["ConnectionStrings:RagProjectDef"];
        var cosmosClientMain = new CosmosClient(connectionString);
        var mainService = new RagDatabaseServiceCosmosDbNoSql(config,
                                                            dateTimeProvider,
                                                            settingsService,
                                                            memCache,
                                                            logger,
                                                            cosmosClientMain,
                                                            chatToolsService);
        _clients.Add(MainRagDatabase, mainService);

        // Copy to service
        string connectionStringCopyTo = config["ConnectionStrings:CopyToRagProjectDef"];
        var cosmosClientCopyTo = new CosmosClient(connectionStringCopyTo);
        var copyToService = new RagDatabaseServiceCosmosDbNoSql(config,
                                                                dateTimeProvider,
                                                                settingsService,
                                                                memCache,
                                                                logger,
                                                                cosmosClientCopyTo,
                                                                chatToolsService);
        _clients.Add(CopyToRagDatabase, copyToService);        
    }

    public IRagDatabaseService GetClient(string name)
    {
        if (_clients.TryGetValue(name, out var client))
            return client;

        // handle error
        throw new ArgumentException(nameof(name));
    }
}
