using Markdig.Extensions.Tables;

namespace ChatUiT2.Models;

public class User
{
    public string Username { get; set; }
    public Preferences Preferences { get; set; }
    public List<WorkItemChat> Chats { get; set; }
    public byte[] AesKey { get; set; }

    public SemaphoreSlim LoadingLock = new(1, 1);

    public User(string username)
    {
        Username = username;
        LoadDummyData();
        // Load preferences
        // Get AES key
        // Load chats
        //throw new Exception("Not implemented");
    }

    private void LoadDummyData()
    {
        Console.WriteLine("Loading dummy data");
        Preferences = new Preferences();
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

