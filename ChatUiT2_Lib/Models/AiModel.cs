using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using iText.StyledXmlParser.Jsoup.Select;
using MudBlazor;
using OpenAI.Chat;
using System.Diagnostics;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

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
    public bool Reasoning = false;
    public bool FunctionCalling = false;

#pragma warning disable OPENAI001
    public ChatReasoningEffortLevel? ReasoningEffortLevel = null;
#pragma warning restore OPENAI001
}


public enum DeploymentType
{
    AzureOpenAI,
}

public enum ModelName
{
    gpt_35,
    gpt_35_turbo,

    gpt_4,
    gpt_4_turbo,

    gpt_4o,
    gpt_4o_mini,

    gpt_41,
    gpt_41_mini,
    gpt_41_nano,

    gpt_45,

    gpt_5,
    gpt_5_chat,
    gpt_5_mini,
    gpt_5_nano,

    o1,
    o1_mini,

    o3,
    o3_low,
    o3_medium,
    o3_high,

    o3_mini_low,
    o3_mini_medium,
    o3_mini_high,

    o4_mini_low,
    o4_mini_medium,
    o4_mini_high,

    dall_e_2,
    dall_e_3,
    text_3_large,
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
#pragma warning disable OPENAI001
        return name switch
        {
            // OpenAI LLMs
            // OpenAI GPT-3.5 models
            ModelName.gpt_35 => new ModelCapabilities { MaxContext = 4096, MaxTokens = 4096, Chat = true },
            ModelName.gpt_35_turbo => new ModelCapabilities { MaxContext = 4096, MaxTokens = 4096, Chat = true },
            // OpenAI GPT-4 models
            ModelName.gpt_4 => new ModelCapabilities { MaxContext = 8138, MaxTokens = 4096, Chat = true },
            ModelName.gpt_4_turbo => new ModelCapabilities { MaxContext = 16_384, MaxTokens = 4096, Chat = true },
            // OpenAI GPT-4o models
            ModelName.gpt_4o => new ModelCapabilities { MaxContext = 128_000, MaxTokens = 16_384, Chat = true, Vision = true },
            ModelName.gpt_4o_mini => new ModelCapabilities { MaxContext = 128_000, MaxTokens = 16_384, Chat = true, Vision = true },
            // GPT-4.1 models
            ModelName.gpt_41 => new ModelCapabilities { MaxContext = 1_047_576, MaxTokens = 32_768, Chat = true, Vision = true },
            ModelName.gpt_41_mini => new ModelCapabilities { MaxContext = 1_047_576, MaxTokens = 32_768, Chat = true, Vision = true },
            ModelName.gpt_41_nano => new ModelCapabilities { MaxContext = 1_047_576, MaxTokens = 32_768, Chat = true, Vision = true },
            // GPT-4.5 models
            ModelName.gpt_45 => new ModelCapabilities { MaxContext = 128_000, MaxTokens = 16_384, Chat = true, Vision = true },
            // Reasoning models

            // GPT-5 models
            ModelName.gpt_5 => new ModelCapabilities { MaxContext = 400_000, MaxTokens = 128_000, Chat = true, Vision = true, Reasoning = true },
            ModelName.gpt_5_chat => new ModelCapabilities { MaxContext = 400_000, MaxTokens = 16_384, Chat = true, Vision = true, Reasoning = true },
            ModelName.gpt_5_mini => new ModelCapabilities { MaxContext = 400_000, MaxTokens = 128_000, Chat = true, Vision = true, Reasoning = true },
            ModelName.gpt_5_nano => new ModelCapabilities { MaxContext = 400_000, MaxTokens = 128_000, Chat = true, Vision = true, Reasoning = true },

            // o1 models
            ModelName.o1 => new ModelCapabilities { MaxContext = 200_000, MaxTokens = 100_000, Chat = true, Vision = true, Reasoning = true, ReasoningEffortLevel = ChatReasoningEffortLevel.High },
            ModelName.o1_mini => new ModelCapabilities { MaxContext = 128_000, MaxTokens = 65_536, Chat = true },
            // o3 models
            ModelName.o3 => new ModelCapabilities { MaxContext = 200_000, MaxTokens = 100_000, Chat = true, Vision = true, Reasoning = true, ReasoningEffortLevel = ChatReasoningEffortLevel.High },
            ModelName.o3_low => new ModelCapabilities { MaxContext = 200_000, MaxTokens = 100_000, Chat = true, Vision = true, Reasoning = true, ReasoningEffortLevel = ChatReasoningEffortLevel.Low },
            ModelName.o3_medium => new ModelCapabilities { MaxContext = 200_000, MaxTokens = 100_000, Chat = true, Vision = true, Reasoning = true, ReasoningEffortLevel = ChatReasoningEffortLevel.Medium },
            ModelName.o3_high => new ModelCapabilities { MaxContext = 200_000, MaxTokens = 100_000, Chat = true, Vision = true, Reasoning = true, ReasoningEffortLevel = ChatReasoningEffortLevel.High },

            ModelName.o3_mini_low => new ModelCapabilities { MaxContext = 200_000, MaxTokens = 100_000, Chat = true, Reasoning = true, ReasoningEffortLevel = ChatReasoningEffortLevel.Low },
            ModelName.o3_mini_medium => new ModelCapabilities { MaxContext = 200_000, MaxTokens = 100_000, Chat = true, Reasoning = true, ReasoningEffortLevel = ChatReasoningEffortLevel.Medium },
            ModelName.o3_mini_high => new ModelCapabilities { MaxContext = 200_000, MaxTokens = 100_000, Chat = true, Reasoning = true, ReasoningEffortLevel = ChatReasoningEffortLevel.High },
            // o4 models
            ModelName.o4_mini_low => new ModelCapabilities { MaxContext = 200_000, MaxTokens = 100_000, Chat = true, Reasoning = true, ReasoningEffortLevel = ChatReasoningEffortLevel.Low },
            ModelName.o4_mini_medium => new ModelCapabilities { MaxContext = 200_000, MaxTokens = 100_000, Chat = true, Reasoning = true, ReasoningEffortLevel = ChatReasoningEffortLevel.Medium },
            ModelName.o4_mini_high => new ModelCapabilities { MaxContext = 200_000, MaxTokens = 100_000, Chat = true, Reasoning = true, ReasoningEffortLevel = ChatReasoningEffortLevel.High },

            // OpenAI DALL-E
            ModelName.dall_e_2 => new ModelCapabilities { Vision = true, ImageGeneration = true },
            ModelName.dall_e_3 => new ModelCapabilities { Vision = false, ImageGeneration = true },

            // Text embeddings
            ModelName.text_3_large => new ModelCapabilities { MaxContext = 50_000, MaxTokens = 50_000, Chat = false },

            _ => throw new NotImplementedException(),
        };
#pragma warning restore OPENAI001
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
