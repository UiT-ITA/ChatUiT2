﻿using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using ChatUiT2.Tools;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace ChatUiT2.Services;

public class UserService : IUserService
{
    public IWorkItem CurrentWorkItem { get; set; }
    public bool IsDarkMode { 
        get => User.Preferences.DarkMode;
        set
        {
            User.Preferences.DarkMode = value;
            _databaseService.SaveUserPreferences(User.Username, User.Preferences);
            _updateService.Update(UpdateType.Global);
        }
    }
    public bool UseMarkdown
    {
        get => User.Preferences.UseMarkdown;
        set
        {
            User.Preferences.UseMarkdown = value;
            _updateService.Update(UpdateType.Global);
            _updateService.Update(UpdateType.ChatMessage);
        }
    }

    public bool SmoothOutput
    {
        get => User.Preferences.SmoothOutput;
        set
        {
            User.Preferences.SmoothOutput = value;
            _updateService.Update(UpdateType.Global);
        }
    }
    public bool Waiting { get; set; } = false;
    public bool Loading { get; set; } = true;
    public string UserName { get => User.Username; }
    public string Name { get; set; } = "Unauthorized";
    public bool IsAdmin { get; set; } = false;
    public bool IsTester { get; set; } = false;
    public bool EnableFileUpload { get; set; } = true;
    private User User { get; set; } = new User();
    private IConfiguration _configuration { get; set; }
    private IChatService _chatService { get; set; }
    public ISettingsService _settingsService { get; set; }
    private IAuthUserService _authUserService { get; set; }
    private IDatabaseService _databaseService { get; set; }
    private IKeyVaultService _keyVaultService { get; set; }
    private IUpdateService _updateService { get; set; }
    //private IStorageService _storageService { get; set; }
    private IJSRuntime _jsRuntime { get; set; }
    private ILogger<UserService> _logger { get; set; }
    private NavigationManager _navigationManager { get; set; }
    public WorkItemChat CurrentChat 
    { 
        get => (WorkItemChat) CurrentWorkItem; 
    }

    public ChatWidth ChatWidth
    {
        get => User.Preferences.ChatWidth;
        set 
        { 
            User.Preferences.ChatWidth = value;
            _databaseService.SaveUserPreferences(User.Username, User.Preferences);
            _updateService.Update(UpdateType.Global);
        }
    }


    SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

    public UserService( IConfiguration configuration, 
                        ISettingsService configService, 
                        IAuthUserService authUserService, 
                        IDatabaseService databaseService,
                        IKeyVaultService keyVaultService,
                        //IStorageService storageService,
                        IUpdateService updateService,
                        IJSRuntime jSRuntime,
                        ILogger<UserService> logger,
                        NavigationManager navigationManager,
                        IChatService chatService)
    {
        _configuration = configuration;
        _settingsService = configService;
        _authUserService = authUserService;
        _databaseService = databaseService;
        _keyVaultService = keyVaultService;
        _jsRuntime = jSRuntime;
        _navigationManager = navigationManager;
        //_storageService = storageService;
        _updateService = updateService;
        _logger = logger;

        _chatService = chatService;

        CurrentWorkItem = new WorkItemChat();

        // Load user
        _ = LoadUser();

        //Console.WriteLine("UserService created");
    }


    public async Task LoadUser()
    {
        if (User.Username != string.Empty)
        {
            return;
        }
        string? username = await _authUserService.GetUsername();

        if (username == null)
        {
            throw new Exception("No user found");
        }        

        User.Username = username;
        Name = await _authUserService.GetName()??"Unauthorized";
        IsAdmin = await _authUserService.TestInRole(["Admin"]);
        IsTester = await _authUserService.TestInRole(["TestUser"]);

        if (_configuration.GetValue<bool>("DBSettings:UseEncryption", defaultValue: false))
        {
            User.AesKey = await _keyVaultService.GetKeyAsync(username);
        }
        
        User.Preferences = await _databaseService.GetUserPreferences(username);

        _updateService.Update(UpdateType.All);

        var workItems = await _databaseService.GetWorkItemListLazy(User, _updateService);
        User.Chats = workItems.OfType<WorkItemChat>().ToList();
        Loading = false;
        NewChat();
        _updateService.Update(UpdateType.Global);
    }

    public void NewChat()
    {
        if (Waiting)
        {
            return;
        }

        var newChat = new WorkItemChat();
        newChat.Persistant = User.Preferences.SaveHistory;
        newChat.Settings.Copy(User.Preferences.DefaultChatSettings);

        SetWorkItem(newChat);
    }

    public void AddChat(WorkItemChat chat)
    {
        User.Chats.Add(chat);
        _updateService.Update(UpdateType.Global);
    }

    public void SetWorkItem(IWorkItem workItem)
    {
        if (Waiting)
        {
            return;
        }
        if (workItem == CurrentWorkItem)
        {
            return;
        }

        if (workItem.State == WorkItemState.Unloaded)
        {
            if (workItem.Type == WorkItemType.Chat)
            {
                workItem.State = WorkItemState.Loading;
                _ = _databaseService.LoadWorkItemComponentsAsync(User, (WorkItemChat)workItem, _updateService);
            }
        }

        if (CurrentWorkItem.Persistant)
        {
            if (CurrentWorkItem.Type == WorkItemType.Chat)
            {
                CurrentChat.State = WorkItemState.Unloaded;
                CurrentChat.Messages = new List<ChatMessage>();
            }
        }

        CurrentWorkItem = workItem;



        if (_navigationManager.Uri != _navigationManager.BaseUri)
        {
            _navigationManager.NavigateTo("/");
        }
        _updateService.Update(UpdateType.Global);
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
            await _databaseService.SaveWorkItem(User, workItem);
        }

        _updateService.Update(UpdateType.Global);
    }

    public async Task SetDefaultChatSettings()
    {
        User.Preferences.DefaultChatSettings.Copy(CurrentChat.Settings);
        User.Preferences.SaveHistory = CurrentChat.Persistant;
        
        await _databaseService.SaveUserPreferences(User.Username, User.Preferences);

        _updateService.Update(UpdateType.Global);

    }

    public List<AiModel> GetModelList()
    {
        return _settingsService.GetModels(this);
    }

    public async void SetPreferredModel(string model)
    {
        User.Preferences.DefaultChatSettings.Model = model;
        await _databaseService.SaveUserPreferences(User.Username, User.Preferences);
    }

    public int GetMaxTokens()
    {
        return GetMaxTokens(CurrentChat);
    }
    public int GetMaxTokens(WorkItemChat chat)
    {
        var model = _settingsService.GetModel(chat.Settings.Model);
        return model.MaxTokens;
    }
    public List<IWorkItem> GetWorkItems()
    {
        return User.Chats.Cast<IWorkItem>()
            .OrderByDescending(x => x.Updated)
            .ToList();
    }

    public async Task LoadWorkItem(IWorkItem workItem)
    {
        // TODO: Implement!
        await Task.Delay(1);
    }

    public async Task UpdateWorkItem()
    {
        await UpdateWorkItem(CurrentWorkItem);
    }

    public async Task UpdateWorkItem(IWorkItem workItem)
    {
        await _databaseService.SaveWorkItem(User, workItem);
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

        if (workItem.Type == WorkItemType.Chat)
        {
            User.Chats.Remove((WorkItemChat)workItem);
        }

        if (!workItem.Persistant)
        {
            return;
        }

        await _databaseService.DeleteWorkItem(User, workItem);
        _updateService.Update(UpdateType.Global);
    }

    public async Task DeleteUser()
    {
        await _databaseService.DeleteUser(User.Username);
        User = new User();
        await LoadUser();
        _updateService.Update(UpdateType.All);
    }


    public async Task SendMessage(string message)
    {
        await SendMessage(message, []);
    }

    public async Task SendMessage(string message, List<ChatFile> files)
    {
        Waiting = true;
        if (!User.Chats.Contains(CurrentChat))
        {
            User.Chats.Add(CurrentChat);
            _updateService.Update(UpdateType.Global);
        }

        foreach (var file in files)
        {
            _logger.LogInformation("Type: {LogType} User: {User} WorkItem {WorkItemId} FileName: {FileName}",
                "FileUpload",
                UserName,
                CurrentChat.Id,
                file.FileName);
        }

        var chatMessage = new ChatMessage
        {
            Role = ChatMessageRole.User,
            Content = message,
            Status = ChatMessageStatus.Done,
            Files = files
        };

        CurrentChat.Messages.Add(chatMessage);
        _updateService.Update(UpdateType.ChatMessage);

        if (CurrentChat.Persistant)
        {
            try
            {
                await _databaseService.SaveChatMessages(User, CurrentChat);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving chat messages for user {User} in work item {WorkItemId}", UserName, CurrentChat.Id);
                CurrentChat.Messages.Remove(chatMessage);
                CurrentChat.Messages.Add(new ChatMessage { Role = ChatMessageRole.Assistant, Content = "Error saving message. You can try creating a new temporary chat", Status = ChatMessageStatus.Error });
                _updateService.Update(UpdateType.ChatMessage);
                try
                {
                    await _databaseService.DeleteChatMessage(chatMessage, CurrentChat, User);
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, "Error deleting missing messages for user {User} in work item {WorkItemId}", UserName, CurrentChat.Id);
                }
                Waiting = false;
                return;
            }
        }

        try
        {
            await SendMessage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message for user {User} in work item {WorkItemId}", UserName, CurrentChat.Id);
            CurrentChat.Messages.Remove(chatMessage);
            CurrentChat.Messages.Add(new ChatMessage { Role = ChatMessageRole.Assistant, Content = "Something went wrong...", Status = ChatMessageStatus.Error });
            if (CurrentChat.Persistant)
            {
                try
                {
                    await _databaseService.DeleteChatMessage(chatMessage, CurrentChat, User);
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, "Error deleting missing messages for user {User} in work item {WorkItemId}", UserName, CurrentChat.Id);
                }
            }
        }
        Waiting = false;
        _updateService.Update(UpdateType.ChatMessage);
    }

    public async Task SendMessage()
    {
        Waiting = true;
        if (CurrentChat.Messages.Count == 0)
        {
            Waiting = false;
            return;
        }

        await _chatService.GetChatResponse(CurrentChat);
        Waiting = false;
        _updateService.Update(UpdateType.ChatMessage);
        _updateService.Update(UpdateType.Global);
    }

    public async Task RegerateFromIndex(int index)
    {
        var chat = CurrentChat;
        Waiting = true;
        List<Task> tasks = new List<Task>();
        for (int i = chat.Messages.Count -1; i > index; i--)
        {
            var message = chat.Messages[i];
            tasks.Add(_databaseService.DeleteChatMessage(message, chat, User));
            chat.Messages.Remove(message);
        }
        await Task.WhenAll(tasks);

        await SendMessage();
    }

    public void StreamUpdated()
    {
        _updateService.Update(UpdateType.ChatMessage);
        //_updateService.Update(UpdateType.Global);
        _jsRuntime.InvokeVoidAsync("forceScroll", "chatContainer");
        ScrollDelayed();
    }

    private async void ScrollDelayed()
    {
        await Task.Delay(200);
        await _jsRuntime.InvokeVoidAsync("forceScroll", "chatContainer");
    }


}
