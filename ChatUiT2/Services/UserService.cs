using ChatUiT2.Models;
using ChatUiT2.Tools;

namespace ChatUiT2.Services;

public class UserService
{
    public IWorkItem CurrentWorkItem { get; set; }
    public bool IsDarkMode { get; set; }
    public bool Waiting { get; set; } = false;
    private User User { get; set; }
    private IConfiguration _configuration { get; set; }
    private ChatService _chatService { get; set; }
    public ConfigService _configService { get; set; }


    public WorkItemChat CurrentChat 
    { 
        get => (WorkItemChat) CurrentWorkItem; 
    }

    public event Action? OnUpdate;

    public UserService(IConfiguration configuration, ConfigService configService)
    {
        _configuration = configuration;
        _configService = configService;


        _chatService = new ChatService(this, configService);

        User = new User("test");
        CurrentWorkItem = new WorkItemChat();
        IsDarkMode = User.Preferences.DarkMode;

    }

    public void RaiseUpdate()
    {
        OnUpdate?.Invoke();
    }

    public void NewChat()
    {
        if (CurrentChat.Messages.Count == 0)
        {
            // Destroy the object?
        }

        CurrentWorkItem = new WorkItemChat();
        CurrentWorkItem.Persistant = User.Preferences.SaveHistory;
        CurrentChat.Settings.Copy(User.Preferences.DefaultChatSettings);
        RaiseUpdate();
    }

    public bool GetSaveHistory()
    {
        return User.Preferences.SaveHistory;
    }

    public async Task SetSaveHistory(IWorkItem workItem, bool value)
    {
        if (workItem.Persistant != value)
        {
            workItem.Persistant = value;
        }

        // TODO: Implement
        await Task.Delay(100);

        RaiseUpdate();
    }

    public async Task SetDefaultChatSettings()
    {
        User.Preferences.DefaultChatSettings.Copy(CurrentChat.Settings);
        User.Preferences.SaveHistory = CurrentChat.Persistant;
        // TODO: Implement
        await Task.Delay(100);
        RaiseUpdate();

    }

    public List<Model> GetModelList()
    {
        return _configService.GetModels();
    }

    public void SetPreferredModel(string model)
    {
        User.Preferences.DefaultChatSettings.Model = model;
    }

    public int GetMaxTokens()
    {
        return GetMaxTokens(CurrentChat);
    }
    public int GetMaxTokens(WorkItemChat chat)
    {
        _configService.GetModel(chat.Settings.Model);
        return 0;
    }
    public List<IWorkItem> GetWorkItems()
    {
        return User.Chats.Cast<IWorkItem>()
            .OrderByDescending(x => x.Updated)
            .ToList();
    }

    public async Task UpdateWorkItem()
    {
        await UpdateWorkItem(CurrentWorkItem);
    }

    public async Task UpdateWorkItem(IWorkItem workItem)
    {
        // TODO: Implement
        await Task.Delay(100);
    }


    public async Task DeleteWorkItem()
    {
        await DeleteWorkItem(CurrentWorkItem);
    }

    public async Task DeleteWorkItem(IWorkItem workItem)
    {

        if (CurrentWorkItem == workItem)
        {
            NewChat();
        }

        if (workItem.Persistant)
        {
            // TODO: Remove from database
        }

        if (workItem.Type == WorkItemType.Chat)
        {
            User.Chats.Remove((WorkItemChat)workItem);
        }

        // TODO: Implement
        await Task.Delay(100);

        Console.WriteLine("Deleting work item: " + workItem.Name);
        RaiseUpdate();
    }

    public async Task SendMessage()
    {
        await SendMessage(null);
    }
    public async Task SendMessage(string? message)
    {
        if (!User.Chats.Contains(CurrentChat))
        {
            User.Chats.Add(CurrentChat);
        }
        await _chatService.GetChatResponse(message);
    }

    public async void UpdateItem(IWorkItem workItem)
    {
        workItem.Updated = DateTimeTools.GetTimestamp();
        // TODO: implement saving
        await Task.Delay(100);
        RaiseUpdate();
    }


}
