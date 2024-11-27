using ChatUiT2.Models;
using Microsoft.JSInterop;
using System.Text.Json;


namespace ChatUiT2.Services;

public class LocalStorageService
{
    private readonly IJSRuntime JSRuntime;
    public LocalStorageService(IJSRuntime jsRuntime)
    {
        JSRuntime = jsRuntime;
    }

    public async Task<string> GetItemAsync(string key)
    {
        return await JSRuntime.InvokeAsync<string>("localStorage.getItem", key);
    }

    public async Task<List<Conversation>> GetConversationHistoryAsync()
    {
        string json = await GetItemAsync("conversationHistory");
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }
        return JsonSerializer.Deserialize<List<Conversation>>(json);
    }

    public async Task<List<WorkItemChat>> GetLocalConversations()
    {
        List<WorkItemChat> conversationHistory = new List<WorkItemChat>();
        Console.WriteLine("Getting history");
        var history = await GetConversationHistoryAsync();
        if (history == null)
        {
            Console.WriteLine("No history found");
            return conversationHistory;
        }
        Console.WriteLine("Got history");
        foreach (var conversation in history)
        {
            Console.WriteLine($"Conversation: {conversation.name}");

            if (conversation.messages.Count == 0)
            {
                continue;
            }

            var settings = new ChatSettings { MaxTokens = 4096, Model = "gpt-4o-mini", Prompt = conversation.prompt, Temperature = (float)conversation.temperature };
            var messages = new List<ChatMessage>();

            messages = conversation.messages.Select(m => new ChatMessage
            {
                Role = m.role == "user" ? ChatMessageRole.User : ChatMessageRole.Assistant,
                Content = m.content,
                Status = ChatMessageStatus.Done
            }).ToList();

            var newConversation = new WorkItemChat
            {
                Name = conversation.name,
                Settings = settings,
                Messages = messages,
                Persistant = false
            };

            conversationHistory.Add(newConversation);
        }


        return conversationHistory;
    }
}

public class Conversation
{
    public string id { get; set; } // Matches "id"
    public string name { get; set; } // Matches "name"
    public List<Message> messages { get; set; } // Matches "messages"
    public OldModel model { get; set; } // Matches "model"
    public string prompt { get; set; } // Matches "prompt"
    public double temperature { get; set; } // Matches "temperature"
    public string folderId { get; set; } // Matches "folderId"
}
public class Message
{
    public string role { get; set; } // Matches "role"
    public string content { get; set; } // Matches "content"
}
public class OldModel
{
    public string id { get; set; } // Matches "id"
    public string name { get; set; } // Matches "name"
    public int maxLength { get; set; } // Matches "maxLength"
    public int tokenLimit { get; set; } // Matches "tokenLimit"
}
