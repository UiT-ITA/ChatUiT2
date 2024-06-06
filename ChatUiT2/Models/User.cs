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

    //public SemaphoreSlim LoadingLock = new(1, 1);


    public void LoadDummyData()
    {
        Console.WriteLine("Loading dummy data");
        Chats = new List<WorkItemChat>();
        Chats.Add(new WorkItemChat { Name = "Chat 1" });
        Chats.Add(new WorkItemChat { Name = "Chat 2" });
        Chats.Add(new WorkItemChat { Name = "Chat 3" });

        AesKey = new byte[32];
    }

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

