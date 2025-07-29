using System.Text.Json.Serialization;
using ChatUiT2.Models.OpenAI;

namespace ChatUiT2.Models.Azure;

public class AzureCompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "text_completion";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = null!;

    [JsonPropertyName("choices")]
    public List<AzureCompletionChoice> Choices { get; set; } = null!;

    [JsonPropertyName("usage")]
    public Usage Usage { get; set; } = null!;
}

public class AzureCompletionChoice
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = null!;

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }

    [JsonPropertyName("logprobs")]
    public object? Logprobs { get; set; }
}
