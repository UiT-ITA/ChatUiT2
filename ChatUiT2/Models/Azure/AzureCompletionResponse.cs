using System.Text.Json.Serialization;

namespace ChatUiT2.Models.Azure;

/// <summary>
/// Azure OpenAI Chat Completions API response model
/// </summary>
public class AzureChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "chat.completion";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = null!;

    [JsonPropertyName("choices")]
    public List<AzureChatChoice> Choices { get; set; } = new();

    [JsonPropertyName("usage")]
    public AzureUsage Usage { get; set; } = new();

    [JsonPropertyName("system_fingerprint")]
    public string? SystemFingerprint { get; set; }
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

    [JsonPropertyName("logprobs")]
    public object? Logprobs { get; set; }
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
