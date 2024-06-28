using ChatUiT2.Interfaces;
using ChatUiT2.Models;

namespace ChatUiT2.Services;

public class ConfigService : IConfigService
{
    private List<Model> models { get; set; } = null!;
    private List<ModelEndpoint> endpoints { get; set; } = null!;
    private Model defaultModel { get; set; } = null!;
    private Model namingModel { get; set; } = null!;


    public ConfigService(IConfiguration configuration)
    {
        ReadModelConfig(configuration);
        //Console.WriteLine("ConfigService created");
    }



    //private void ReadConfig(IConfiguration configuration, KeyVaultService keyVaultService)
    private void ReadModelConfig(IConfiguration configuration)
    {
        var modelSection = configuration.GetSection("Models");
        models = modelSection.Get<List<Model>>() ?? new List<Model>();

        if (models.Count == 0)
        {
            throw new Exception("No models found in configuration!");
        }

        string defaultModelName = configuration["DefaultModel"] ?? models[0].Name;
        string namingModelName = configuration["NamingModel"] ?? models[0].Name;

        defaultModel = models.FirstOrDefault(m => m.Name == defaultModelName) ?? models[0];
        namingModel = models.FirstOrDefault(m => m.Name == namingModelName) ?? models[0];

        var endpointSection = configuration.GetSection("Endpoints");
        endpoints = endpointSection.Get<List<ModelEndpoint>>() ?? new List<ModelEndpoint>();

        foreach (var endpoint in endpoints)
        {
            endpoint.Key = configuration[endpoint.Name] ?? "";
            if (endpoint.Key == "")
            {
                throw new Exception($"No key found for endpoint {endpoint.Name}");
            }
        }

        foreach (var model in models)
        {
            var endpoint = endpoints.FirstOrDefault(e => e.Name == model.Deployment);
            if (endpoint == null)
            {

               throw new Exception($"No endpoint found for model {model.Name}");
            }
        }
    }

    public List<Model> GetModels()
    {
        return models;
    }

    public Model GetDefaultModel()
    {
        return defaultModel;
    }

    public Model GetNamingModel()
    {
        return namingModel;
    }

    public Model GetModel(string name)
    {
        return models.FirstOrDefault(m => m.Name == name) ?? defaultModel;
    }

    public ModelEndpoint GetEndpoint(string name)
    {

        var endpoint = endpoints.FirstOrDefault(e => e.Name == name);
        if (endpoint == null)
        {
            throw new Exception($"No endpoint found for model {name}");
        }
        return endpoint;
    }

    public ModelEndpoint GetEndpoint(Model model)
    {
        return GetEndpoint(model.DeploymentName);
    }
    
}
