using ChatUiT2.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ChatUiT2.Services;

public static class GroqService
{
    public static async Task<string> GetResponse(string apiKey)
    {
        GroqApiClient client = new(apiKey);
        JsonObject request = new()
        {
            ["model"] = "Llama3-70b-8192",
            //["model"] = "mixtral-8x7b-32768",
            ["temperature"] = 0.5,
            ["max_tokens"] = 1024,
            ["top_p"] = 1,
            ["stop"] = "TERMINATE",
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "system",
                    ["content"] = "You are a helpful assistant."
                },
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = "What is the capital of the United States?"
                }
            }
        };

        JsonObject? result = await client.CreateChatCompletionAsync(request);
        string response = result?["choices"]?[0]?["message"]?["content"]?.ToString() ?? "No response found";
        return response;
    }

    public static IAsyncEnumerable<JsonObject?> GetStreamingResponse(WorkItemChat chat, AiModel model, ModelEndpoint endpoint)
    {
        GroqApiClient client = new(endpoint.Key);

        JsonArray messages = new()
        {
            new JsonObject
            {
                ["role"] = "system",
                ["content"] = chat.Settings.Prompt
            }
        };

        foreach (var message in chat.Messages)
        {
            JsonObject chatMessage = CreateChatRequestMessage(message);
            messages.Add(chatMessage);
        }

        JsonObject request = new()
        {
            ["model"] = model.DeploymentName,
            ["temperature"] = chat.Settings.Temperature,
            ["max_tokens"] = chat.Settings.MaxTokens,
            ["top_p"] = 1,
            ["stop"] = "TERMINATE",
            ["messages"] = messages
        };

        return client.CreateChatCompletionStreamAsync(request);
    }

    private static JsonObject CreateChatRequestMessage(ChatMessage message)
    {
        string role;
        if (message.Role == ChatMessageRole.User)
        {
            role = "user";
        }
        else if (message.Role == ChatMessageRole.Assistant)
        {
            role = "assistant";
        }
        else
        {
            throw new Exception("Unknown message role");
        }

        return new JsonObject
        {
            ["role"] = role,
            ["content"] = message.Content
        };
    }
}



public class GroqApiClient
{
    private readonly HttpClient client = new();

    public GroqApiClient(string apiKey)
    {
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<JsonObject?> CreateChatCompletionAsync(JsonObject request)
    {
        // Commented out until stabilized on Groq
        // the API is still not accepting the request payload in its documented format, even after following the JSON mode instructions.
        // request.Add("response_format", new JsonObject(new KeyValuePair<string, JsonNode?>("type", "json_object")));

        StringContent httpContent = new(request.ToJsonString(), Encoding.UTF8, MediaTypeHeaderValue.Parse("application/json"));

        HttpResponseMessage response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", httpContent);
        response.EnsureSuccessStatusCode();

        string responseString = await response.Content.ReadAsStringAsync();
        JsonObject? responseJson = JsonSerializer.Deserialize<JsonObject>(responseString);

        return responseJson;
    }

    public async IAsyncEnumerable<JsonObject?> CreateChatCompletionStreamAsync(JsonObject request)
    {
        request.Add("stream", true);

        StringContent httpContent = new(request.ToJsonString(), Encoding.UTF8, MediaTypeHeaderValue.Parse("application/json"));

        HttpResponseMessage response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", httpContent);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            String? line = await reader.ReadLineAsync();
            if (line is not null && line.StartsWith("data: "))
            {
                var data = line["data: ".Length..];
                if (data != "[DONE]")
                {
                    yield return JsonSerializer.Deserialize<JsonObject>(data);
                }
            }
        }
    }
}