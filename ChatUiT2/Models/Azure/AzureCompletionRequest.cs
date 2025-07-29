using System.Text.Json.Serialization;

namespace ChatUiT2.Models.Azure;

/// <summary>
/// Azure OpenAI Chat Completions API request model
/// </summary>
public class AzureChatCompletionRequest
{
    [JsonPropertyName("messages")]
    public List<AzureChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;

    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public float? TopP { get; set; }

    [JsonPropertyName("frequency_penalty")]
    public float? FrequencyPenalty { get; set; }

    [JsonPropertyName("presence_penalty")]
    public float? PresencePenalty { get; set; }

    [JsonPropertyName("stop")]
    public object? Stop { get; set; }

    [JsonPropertyName("user")]
    public string? User { get; set; }

    [JsonPropertyName("n")]
    public int? N { get; set; }

    [JsonPropertyName("logit_bias")]
    public object? LogitBias { get; set; }

    [JsonPropertyName("logprobs")]
    public bool? Logprobs { get; set; }

    [JsonPropertyName("top_logprobs")]
    public int? TopLogprobs { get; set; }

    [JsonPropertyName("seed")]
    public int? Seed { get; set; }
}

/// <summary>
/// Azure OpenAI Chat Message model
/// </summary>
public class AzureChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = null!;

    [JsonPropertyName("content")]
    public string Content { get; set; } = null!;

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
