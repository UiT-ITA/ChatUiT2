using System.Text.Json.Serialization;

namespace ChatUiT2.Models.Azure;

public class AzureStreamCompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("choices")]
    public List<AzureChatDelta> Choices { get; set; } = new();
}

/// <summary>
/// Azure OpenAI Chat Choice model
/// </summary>
public class AzureChatDelta
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("delta")]
    public AzureStreamContent Delta { get; set; } = null!;

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// Azure OpenAI Usage model
/// </summary>
public class AzureStreamContent
{
    [JsonPropertyName("content")]
    public string Content { get; set; } =   string.Empty;
}
