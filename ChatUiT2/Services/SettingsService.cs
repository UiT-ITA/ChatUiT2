using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using Microsoft.EntityFrameworkCore.Metadata;
using MudBlazor;

namespace ChatUiT2.Services;

public class SettingsService : ISettingsService
{
    private List<AiModel> models { get; set; } = null!;
    private List<ModelEndpoint> endpoints { get; set; } = null!;
    private AiModel defaultModel { get; set; } = null!;
    private AiModel namingModel { get; set; } = null!;

    private readonly IConfiguration _configuration;


    public SettingsService(IConfiguration configuration)
    {
        _configuration = configuration;
        ReadModelsFromConfig();
        ReadModelsFromDatabase();
        //Console.WriteLine("ConfigService created");
    }

    public void ReadModelsFromConfig()
    {
        var endpointSection = _configuration.GetSection("Endpoints");
        var modelSection = _configuration.GetSection("Models");

        if (modelSection == null)
        {
            throw new Exception("No models found in configuration!");
        }
        if (endpointSection == null)
        {
            throw new Exception("No endpoints found in configuration!");
        }

        var endpoints = endpointSection.Get<List<ModelEndpoint>>();
        if (endpoints == null || endpoints.Count == 0)
        {
            throw new Exception("No endpoints found in configuration!");
        }

        var models = modelSection.Get<List<Dictionary<string, object>>>();
        if (models == null || models.Count == 0)
        {
            throw new Exception("No models found in configuration!");
        }

        var aiModels = new List<AiModel>();

        foreach (var modelData in models)
        {
            var displayName = modelData["DisplayName"].ToString();
            var description = modelData["Description"].ToString();
            var icon = modelData["Icon"].ToString() ?? Icons.Material.Filled.Build;
            var configModelName = modelData["ModelName"].ToString();
            var deploymentName = modelData["DeploymentName"].ToString();
            var deploymentEndpoint = modelData["DeploymentEndpoint"].ToString();
            var deploymentType = modelData["DeploymentType"].ToString();

            if (string.IsNullOrEmpty(displayName)) throw new Exception("No display name found for model");
            if (string.IsNullOrEmpty(description)) throw new Exception("No description found for model");
            if (string.IsNullOrEmpty(deploymentName)) throw new Exception("No deployment name found for model");
            if (string.IsNullOrEmpty(deploymentEndpoint)) throw new Exception("No deployment endpoint found for model");
            if (string.IsNullOrEmpty(deploymentType)) throw new Exception("No deployment type found for model");
            if (string.IsNullOrEmpty(configModelName)) throw new Exception("No model name found for model");


            var modelName = MapModelName(configModelName);
            var endpoint = endpoints.FirstOrDefault(e => e.Name == deploymentEndpoint);
            if (endpoint == null)
            {
                throw new Exception($"No endpoint found for model {displayName}");
            }

            var aiModel = new AiModel
            {
                DisplayName = displayName,
                Description = description,
                Icon = MapIconName(icon),
                ModelName = modelName,
                DeploymentType = deploymentType,
                DeploymentName = deploymentName,
                Endpoint = endpoint.Url,
                ApiKey = endpoint.Key,
                MaxContext = 0,
                MaxTokens = 0
            }

        }







        string defaultModelName = _configuration["DefaultModel"] ?? models[0].DisplayName;
        string namingModelName = _configuration["NamingModel"] ?? models[0].DisplayName;
    }

    public ModelName MapModelName(string modelName)
    {
        return modelName switch
        {
            "gpt-3.5" => ModelName.gpt35,
            "gpt-3.5-turbo" => ModelName.gpt35turbo,
            "gpt-4" => ModelName.gpt4,
            "gpt-4-turbo" => ModelName.gpt4turbo,
            "gpt-4o" => ModelName.gpt4o,
            "gpt-4o-mini" => ModelName.gpt4omini,
            "o1" => ModelName.o1,
            "o1-mini" => ModelName.o1mini,
            "dalle2" => ModelName.dalle2,
            "dalle3" => ModelName.dalle3,
            _ => throw new Exception($"Unknown model name {modelName}")
        };
    }

    private string MapIconName(string iconName)
    {
        return iconName switch
        {
            "HotelClass" => Icons.Material.Filled.HotelClass,
            "Star" => Icons.Material.Filled.Star,
            "StarBorder" => Icons.Material.Filled.StarBorder,
            "StarHalf" => Icons.Material.Filled.StarHalf,
            "StarOutline" => Icons.Material.Filled.StarOutline,
            "StarRate" => Icons.Material.Filled.StarRate,
            "Build" => Icons.Material.Filled.Build,
            "PersonSearch" => Icons.Material.Filled.PersonSearch,
            _ => Icons.Material.Filled.QuestionMark
        };
    }

    public void ReadModelsFromDatabase()
    {
    }

    //private void ReadConfig(IConfiguration configuration, KeyVaultService keyVaultService)
    //private void ReadModelConfig(IConfiguration configuration)
    //{
    //    var modelSection = configuration.GetSection("Models");
    //    models = modelSection.Get<List<AiModel>>() ?? new List<AiModel>();

        //    if (models.Count == 0)
        //    {
        //        throw new Exception("No models found in configuration!");
        //    }

        //    string defaultModelName = configuration["DefaultModel"] ?? models[0].DisplayName;
        //    string namingModelName = configuration["NamingModel"] ?? models[0].DisplayName;

        //    defaultModel = models.FirstOrDefault(m => m.Name == defaultModelName) ?? models[0];
        //    namingModel = models.FirstOrDefault(m => m.Name == namingModelName) ?? models[0];

        //    var endpointSection = configuration.GetSection("Endpoints");
        //    endpoints = endpointSection.Get<List<ModelEndpoint>>() ?? new List<ModelEndpoint>();

        //    foreach (var endpoint in endpoints)
        //    {
        //        endpoint.Key = configuration[endpoint.Name] ?? "";
        //        if (endpoint.Key == "")
        //        {
        //            throw new Exception($"No key found for endpoint {endpoint.Name}");
        //        }
        //    }

        //    foreach (var model in models)
        //    {
        //        var endpoint = endpoints.FirstOrDefault(e => e.Name == model.Deployment);
        //        if (endpoint == null)
        //        {

        //           throw new Exception($"No endpoint found for model {model.Name}");
        //        }
        //    }
        //}

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
