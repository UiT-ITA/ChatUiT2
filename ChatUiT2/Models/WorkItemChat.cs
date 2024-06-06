using ChatUiT2.Interfaces;
using ChatUiT2.Tools;
using System.Text.Json.Serialization;

namespace ChatUiT2.Models;

public class WorkItemChat : IWorkItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New chat";
    public WorkItemType Type { get; set; } = WorkItemType.Chat;
    public DateTime Created { get; set; } = DateTimeTools.GetTimestamp();
    public DateTime Updated { get; set; } = DateTimeTools.GetTimestamp();
    public bool IsFavorite { get; set; } = false;
    [JsonIgnore]
    public bool Persistant { get; set; } = true;

    public ChatSettings Settings { get; set; } = new ChatSettings();

    [JsonIgnore]
    public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public class ChatMessage
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public ChatMessageRole Role { get; set; } = ChatMessageRole.User;
    public string Content { get; set; } = string.Empty;
    public ChatMessageStatus Status { get; set; } = ChatMessageStatus.Working;
    public DateTime Created { get; set; } = DateTimeTools.GetTimestamp();

}

public enum  ChatMessageRole
{
    User,
    Assistant,
    System,
    Tool
}

public enum ChatMessageStatus
{
    Working,
    Done,
    Error
}