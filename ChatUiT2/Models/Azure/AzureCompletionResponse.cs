using System.Text.Json.Serialization;

namespace ChatUiT2.Models.Azure;

/// <summary>
/// Azure OpenAI Chat Completions API response model
/// </summary>
public class AzureChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("choices")]
    public List<AzureChatChoice> Choices { get; set; } = new();

    [JsonPropertyName("usage")]
    public AzureUsage Usage { get; set; } = new();
}

/// <summary>
/// Azure OpenAI Chat Choice model
/// </summary>
public class AzureChatChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public AzureChatMessage Message { get; set; } = null!;

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// Azure OpenAI Usage model
/// </summary>
public class AzureUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}
