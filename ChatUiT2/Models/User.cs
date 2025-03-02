using ChatUiT2.Interfaces;
using Markdig.Extensions.Tables;
using MongoDB.Bson.Serialization.Attributes;

namespace ChatUiT2.Models;

public class User
{
    [BsonId]
    public string Username { get; set; } = string.Empty;
    public Preferences Preferences { get; set; } = new Preferences();
    public List<WorkItemChat> Chats { get; set; } = [];
    public byte[]? AesKey { get; set; } = null;

    public void SetItems(List<IWorkItem> workItems)
    {
        Chats = workItems.OfType<WorkItemChat>().ToList();
    }

    public void AddItem(IWorkItem workItem)
    {
        if (workItem is WorkItemChat chat)
        {
            Chats.Add(chat);
        }
        else
        {
            throw new Exception("Unknown work item type");
        }
    }
}

public enum UserRole
{
    User,
    Admin,
    BetaTester,
    External
}

