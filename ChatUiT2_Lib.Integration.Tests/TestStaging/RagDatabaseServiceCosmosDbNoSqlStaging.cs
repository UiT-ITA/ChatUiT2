using ChatUiT2.Interfaces;
using ChatUiT2.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ChatUiT2.Integration.Tests.TestStaging;
public static class RagDatabaseServiceCosmosDbNoSqlStaging
{
    public static IRagDatabaseService GetRagDatabaseServiceCosmosDbNoSqlStaging(string environment)
    {
        var host = HostBuilderStaging.GetHost(environment);
        return host.Services.GetRequiredService<IRagDatabaseService>();
    }        
}
