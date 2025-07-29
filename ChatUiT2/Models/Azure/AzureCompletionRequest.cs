using System.Text.Json.Serialization;

namespace ChatUiT2.Models.Azure;

public class AzureCompletionRequest
{
    [JsonPropertyName("prompt")]
    public object Prompt { get; set; } = null!; // Can be string or array

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;

    // All other properties are ignored - they exist for compatibility but we don't use them
    [JsonPropertyName("best_of")]
    public int? BestOf { get; set; }

    [JsonPropertyName("echo")]
    public bool? Echo { get; set; }

    [JsonPropertyName("frequency_penalty")]
    public float? FrequencyPenalty { get; set; }

    [JsonPropertyName("logit_bias")]
    public object? LogitBias { get; set; }

    [JsonPropertyName("logprobs")]
    public int? Logprobs { get; set; }

    [JsonPropertyName("n")]
    public int? N { get; set; }

    [JsonPropertyName("presence_penalty")]
    public float? PresencePenalty { get; set; }

    [JsonPropertyName("seed")]
    public int? Seed { get; set; }

    [JsonPropertyName("stop")]
    public object? Stop { get; set; }

    [JsonPropertyName("suffix")]
    public string? Suffix { get; set; }

    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public float? TopP { get; set; }

    [JsonPropertyName("user")]
    public string? User { get; set; }
}
