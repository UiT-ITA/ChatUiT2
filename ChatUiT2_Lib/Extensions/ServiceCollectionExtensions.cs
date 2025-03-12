using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatUiT2.Extensions;

public static class ServiceCollectionExtensions
{
    private static readonly Dictionary<string, object> _namedSingletons = new Dictionary<string, object>();

    public static IServiceCollection AddKeyedSingleton<TService>(this IServiceCollection services, string key, Func<IServiceProvider, TService> implementationFactory) where TService : class
    {
        services.AddSingleton(provider =>
        {
            if (!_namedSingletons.ContainsKey(key))
            {
                _namedSingletons[key] = implementationFactory(provider);
            }
            return (TService)_namedSingletons[key];
        });
        return services;
    }

    public static TService GetKeyedSingleton<TService>(this IServiceProvider provider, string key) where TService : class
    {
        return (TService)_namedSingletons[key];
    }
}
