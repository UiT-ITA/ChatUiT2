using MudBlazor;

namespace ChatUiT2.Models;

public class OldModel
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string DeploymentName { get; set; } = null!;
    public string DeploymentType { get; set; } = null!;
    public string Deployment { get; set; } = null!;
    public int MaxContext { get; set; }
    public int MaxTokens { get; set; }
}

public class ModelEndpoint
{
    public string Name { get; set; } = null!;
    public string Url { get; set; } = null!;
    public string Key { get; set; } = null!;
}

public class AzureOpenaiEndpoint : ModelEndpoint
{
    public string DeploymentName { get; set; } = null!;

}
    public class AiModel
{
    // Displayed to user
    public string DisplayName { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Icon { get; set; } = Icons.Material.Filled.Build;
    public ModelName ModelName { get; set; }


    // Used internally
    public DeploymentType DeploymentType { get; set; }
    public string Endpoint { get; set; } = null!;
    public string ApiKey { get; set; } = null!;
    public string DeploymentName { get; set; } = null!;

    // Capabilities
    public ModelCapabilities Capabilities { get; set; } = new ModelCapabilities();
    public int MaxContext { get; set; }
    public int MaxTokens { get; set; }

    // Settings

    public List<ChatToolDescription> OptionalTools { get; set; } = new List<ChatToolDescription>();
    public List<ChatToolDescription> RequiredTools { get; set; } = new List<ChatToolDescription>();

    // Restrictions
    public List<string> AllowedRoles { get; set; } = new List<string>(); // If none set, all roles are allowed
    public bool AllowCustomPrompt { get; set; } = true;

}

public class  ModelCapabilities
{
    public int MaxContext { get; set; } = 0;
    public int MaxTokens { get; set; } = 0;

    public bool Chat = false;
    public bool Vision = false;
    public bool ImageGeneration = false;
    public bool VoiceInput = false;
    public bool VoiceOutput = false;
    public bool VideoInput = false;
    public bool VideoOutput = false;
}


public enum DeploymentType
{
    AzureOpenAI,
}

public enum ModelName
{
    gpt35,
    gpt35turbo,
    gpt4,
    gpt4turbo,
    gpt4o,
    gpt4omini,
    o1,
    o1mini,
    dalle2,
    dalle3,
}

public static class ModelServiceExtensions
{
    public static string GetDisplayName(this DeploymentType service)
    {
        return service switch
        {
            DeploymentType.AzureOpenAI => "Azure OpenAI",
            _ => throw new NotImplementedException(),
        };
    }

    public static ModelCapabilities GetCapabilities(this ModelName name)
    {
        return name switch
        {
            // OpenAI LLMs
            ModelName.gpt35 => new ModelCapabilities        { MaxContext = 4096, MaxTokens = 4096, Chat = true },
            ModelName.gpt35turbo => new ModelCapabilities   { MaxContext = 4096, MaxTokens = 4096, Chat = true },
            ModelName.gpt4 => new ModelCapabilities         { MaxContext = 8138, MaxTokens = 4096, Chat = true },
            ModelName.gpt4turbo => new ModelCapabilities    { MaxContext = 16_384, MaxTokens = 4096, Chat = true },
            ModelName.gpt4o => new ModelCapabilities        { MaxContext = 128_000, MaxTokens = 4096, Chat = true, Vision = true },
            ModelName.gpt4omini => new ModelCapabilities    { MaxContext = 128_000, MaxTokens = 16_384, Chat = true, Vision = true },
            ModelName.o1 => new ModelCapabilities       { MaxContext = 128_000, MaxTokens = 4096, Chat = true, Vision = true },
            ModelName.o1mini => new ModelCapabilities   { MaxContext = 128_000, MaxTokens = 4096, Chat = true, Vision = true },

            // OpenAI DALL-E
            ModelName.dalle2 => new ModelCapabilities { Vision = true, ImageGeneration = true },
            ModelName.dalle3 => new ModelCapabilities { Vision = false, ImageGeneration = true },

            _ => throw new NotImplementedException(),
        };
    }
}

public class ModelConfig
{
    public string DisplayName { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? Icon { get; set; }
    public string DeploymentName { get; set; } = null!;
    public string DeploymentType { get; set; } = null!;
    public string ModelName { get; set; } = null!;
    public string DeploymentEndpoint { get; set; } = null!;
    public List<string> OptionalTools { get; set; } = new List<string>();
    public List<string> RequiredTools { get; set; } = new List<string>();
    public List<string> AllowedRoles { get; set; } = new List<string>();
    public int MaxContext { get; set; } = 0;
    public int MaxTokens { get; set; } = 0;
    public bool AllowCustomPrompt { get; set; } = true;

    // Add other properties as needed
}
