using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using ChatUiT2.Tools;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using MudBlazor;
using OpenAI.Chat;
using System.Text.Json;

namespace ChatUiT2.Services;

public class SettingsService : ISettingsService
{
    public List<AiModel> Models { get; set; } = new List<AiModel>();
    private List<ModelEndpoint> _endpoints { get; set; } = new List<ModelEndpoint>();
    public AiModel DefaultModel { get; set; } = null!;
    public AiModel NamingModel { get; set; } = null!;
    public AiModel EmbeddingModel { get; set; } = null!;
    public AiModel ImageModel { get; set; } = null!;

    private readonly IConfiguration _configuration;

    public SettingsService(IConfiguration configuration)
    {
        _configuration = configuration;
        ReadModelsFromConfig();
        ReadModelsFromDatabase();

    }

    public void ReadModelsFromConfig()
    {
        var modelSection = _configuration.GetSection("Models");
        var endpointSection = _configuration.GetSection("Endpoints");

        var endpoints = endpointSection.Get<List<ModelEndpoint>>();
        if (endpoints == null || endpoints.Count == 0)
        {
            throw new Exception("No endpoints found in configuration!");
        }
        foreach (var endpoint in endpoints)
        {
            endpoint.Key = _configuration[endpoint.Name] ?? "";
            if (endpoint.Key == "")
            {
                throw new Exception($"No key found for endpoint {endpoint.Name}");
            }
        }

        var models = modelSection.Get<List<ModelConfig>>();
        if (models == null || models.Count == 0)
        {
            throw new Exception("No models found in configuration!");
        }

        foreach (var model in models)
        {
            var endpoint = endpoints.FirstOrDefault(e => e.Name == model.DeploymentEndpoint);
            if (endpoint == null)
            {
                throw new Exception($"No endpoint found for model {model.DisplayName}");
            }
            var modelName = MapModelName(model.ModelName);
            var capabillities = modelName.GetCapabilities();


            var maxContext = capabillities.MaxContext;
            if (model.MaxContext > 0 && model.MaxContext < capabillities.MaxContext)
            {
                maxContext = model.MaxContext;
            }

            var maxTokens = capabillities.MaxTokens;
            if (model.MaxTokens > 0 && model.MaxTokens < capabillities.MaxTokens)
            {
                maxTokens = model.MaxTokens;
            }

            AiModel aiModel = new AiModel
            {
                DisplayName = model.DisplayName,
                Description = model.Description,
                Icon = string.IsNullOrEmpty(model.Icon) ? Icons.Material.Filled.Build : MapIconName(model.Icon),
                ModelName = modelName,

                DeploymentType = MapDeploymentType(model.DeploymentType),
                Endpoint = endpoint.Url,
                ApiKey = endpoint.Key,
                DeploymentName = model.DeploymentName,

                Capabilities = capabillities,
                MaxContext = maxContext,
                MaxTokens = maxTokens,

                AllowedRoles = model.AllowedRoles,
                
                OptionalTools = MapTools(model.OptionalTools),
                RequiredTools = MapTools(model.RequiredTools),

                AllowCustomPrompt = model.AllowCustomPrompt
            };

            Models.Add(aiModel);

        }

        string? defaultModelName = _configuration["DefaultModel"];
        string? namingModelName = _configuration["NamingModel"];
        string? embeddingModelName = _configuration["EmbeddingModel"];
        string? imageModelName = _configuration["ImageModel"];

        DefaultModel = Models.FirstOrDefault(m => m.DisplayName == defaultModelName) ?? Models[0];
        NamingModel = Models.FirstOrDefault(m => m.DisplayName == namingModelName) ?? Models[0];
        EmbeddingModel = Models.FirstOrDefault(m => m.DisplayName == embeddingModelName) ?? Models[0];
        ImageModel = Models.FirstOrDefault(m => m.DisplayName == embeddingModelName) ?? Models[0];
    }

    public ModelName MapModelName(string modelName)
    {
        return modelName switch
        {
            "gpt-3.5" => ModelName.gpt_35,
            "gpt-3.5-turbo" => ModelName.gpt_35_turbo,
            "gpt-4" => ModelName.gpt_4,
            "gpt-4-turbo" => ModelName.gpt_4_turbo,
            "gpt-4o" => ModelName.gpt_4o,
            "gpt-4o-mini" => ModelName.gpt_4o_mini,
            "o1" => ModelName.o1,
            "o1-mini" => ModelName.o1_mini,
            "o3-mini-low" => ModelName.o3_mini_low,
            "o3-mini-medium" => ModelName.o3_mini_medium,
            "o3-mini-high" => ModelName.o3_mini_high,
            "dalle2" => ModelName.dall_e_2,
            "dalle3" => ModelName.dall_e_3,
            "text-3-large" => ModelName.text_3_large,
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
            "HourglassBottom" => Icons.Material.Filled.HourglassBottom,
            "HourglassFull" => Icons.Material.Filled.HourglassFull,
            "Image" => Icons.Material.Filled.Image,
            _ => Icons.Material.Filled.QuestionMark
        };
    }

    private DeploymentType MapDeploymentType(string deploymentType)
    {
        return deploymentType switch
        {
            "AzureOpenAI" => DeploymentType.AzureOpenAI,
            _ => throw new Exception($"Unknown deployment type {deploymentType}")
        };
    }

    private List<ChatToolDescription> MapTools(List<string> tools)
    {
        var toolDescriptions = new List<ChatToolDescription>();

        foreach (var tool in tools)
        {
            Console.WriteLine($"Mapping tool {tool}");
            var toolDescription = ChatTools.Tools.FirstOrDefault(t => t.DisplayName == tool);
            if (toolDescription != null)
            {
                toolDescriptions.Add(toolDescription);
            }
        }
        return toolDescriptions;
    }

    public void ReadModelsFromDatabase()
    {
    }

    public AiModel GetModel(string name)
    {
        return Models.FirstOrDefault(m => m.DisplayName == name) ?? DefaultModel;
    }    
}
