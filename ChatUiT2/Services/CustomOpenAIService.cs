using System.Text;
using System.Text.Json;

namespace ChatUiT2.Services;

public class CustomOpenAIService
{
    private readonly HttpClient _inferenceClient;
    public string ManagementApiVersion { get; set; }
    public string InferenceApiVersion { get; set; }

    // For inference (completions, streaming, etc.)
    public CustomOpenAIService(string resourceEndpoint, string apiKey, string inferenceApiVersion = "2025-01-01-preview", string managementApiVersion = "2025-01-01-preview")
    {
        _inferenceClient = new HttpClient { BaseAddress = new Uri(resourceEndpoint) };
        _inferenceClient.DefaultRequestHeaders.Add("api-key", apiKey);
        InferenceApiVersion = inferenceApiVersion;
        ManagementApiVersion = managementApiVersion;
    }

    // Non-streaming completion call
    public async Task<string> GetCompletionAsync(string deploymentId, string prompt, int maxTokens = 100)
    {
        var uri = $"/openai/deployments/{deploymentId}/completions?api-version={_inferenceApiVersion}";
        var requestBody = new
        {
            prompt = prompt,
            max_tokens = maxTokens
        };
        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _inferenceClient.PostAsync(uri, content);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseContent);
        // Assumes response shape: { "choices": [ { "text": "..." } ] }
        return doc.RootElement.GetProperty("choices")[0].GetProperty("text").GetString();
    }

    // Streaming completion call
    public async Task StreamCompletionAsync(string deploymentId, string prompt, Action<string> onData, int maxTokens = 100)
    {
        var uri = $"/openai/deployments/{deploymentId}/completions?api-version={InferenceApiVersion}";
        var requestBody = new
        {
            prompt = prompt,
            max_tokens = maxTokens,
            stream = true
        };
        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
        using var response = await _inferenceClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("data: "))
            {
                var data = line.Substring("data: ".Length).Trim();
                if (data == "[DONE]") break;
                using var doc = JsonDocument.Parse(data);
                var text = doc.RootElement.GetProperty("choices")[0].GetProperty("text").GetString();
                onData(text);
            }
        }
    }

    // List deployments (available models) using the management API.
    // This endpoint requires your subscription ID, resource group, and account name.
    public static async Task<List<string>> ListDeploymentsAsync(
        string subscriptionId,
        string resourceGroupName,
        string accountName,
        string managementApiKey,
        string managementApiVersion = "2023-05-01")
    {
        // Management endpoint for listing deployments:
        var managementUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.CognitiveServices/accounts/{accountName}/deployments?api-version={managementApiVersion}";
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("api-key", managementApiKey);
        var response = await client.GetAsync(managementUrl);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var deployments = new List<string>();
        // The response shape is assumed to be: { "value": [ { "name": "deploymentId", ... } ] }
        foreach (var element in doc.RootElement.GetProperty("value").EnumerateArray())
        {
            var depId = element.GetProperty("name").GetString();
            deployments.Add(depId);
        }
        return deployments;
    }

}

