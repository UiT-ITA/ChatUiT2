using ChatUiT2.Interfaces;
using ChatUiT2.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ChatUiT2.Integration.Tests.TestStaging;
public static class RagDatabaseServiceCosmosDbNoSqlStaging
{
    public static IRagDatabaseService GetRagDatabaseServiceCosmosDbNoSqlStaging(string environment,
                                                                                IDateTimeProvider? dateTimeProvider = null)
    {
        var host = HostBuilderStaging.GetHost(environment, dateTimeProvider);
        return host.Services.GetRequiredService<IRagDatabaseService>();
    }        
}
