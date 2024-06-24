﻿using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using ChatUiT2.Tools;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Configuration;

namespace ChatUiT2.Services;

public class UserService : IUserService
{
    public IWorkItem CurrentWorkItem { get; set; }
    public bool IsDarkMode { 
        get => User.Preferences.DarkMode;
        set
        {
            User.Preferences.DarkMode = value;
            RaiseUpdate();
        }
    }
    public bool UseMarkdown
    {
        get => User.Preferences.UseMarkdown;
        set
        {
            User.Preferences.UseMarkdown = value;
            RaiseUpdate();
        }
    }

    public bool SmoothOutput
    {
        get => User.Preferences.SmoothOutput;
        set
        {
            User.Preferences.SmoothOutput = value;
            RaiseUpdate();
        }
    }
    public bool Waiting { get; set; } = false;
    public bool Loading { get; set; } = true;
    public string Name { get; set; } = "Unauthorized";
    public bool IsAdmin { get; set; } = false;
    public bool EnableFileUpload { get; set; } = false;
    private User User { get; set; } = new User();
    private IConfiguration _configuration { get; set; }
    private IChatService _chatService { get; set; }
    public IConfigService _configService { get; set; }
    private IAuthUserService _authUserService { get; set; }
    private IDatabaseService _databaseService { get; set; }
    private IKeyVaultService _keyVaultService { get; set; }
    private IJSRuntime _jsRuntime { get; set; }
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
            RaiseUpdate();
        }
    }

    public event Action? OnUpdate;

    SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

    public UserService( IConfiguration configuration, 
                        IConfigService configService, 
                        IAuthUserService authUserService, 
                        IDatabaseService databaseService,
                        IKeyVaultService keyVaultService,
                        IJSRuntime jSRuntime,
                        NavigationManager navigationManager)
    {
        _configuration = configuration;
        _configService = configService;
        _authUserService = authUserService;
        _databaseService = databaseService;
        _keyVaultService = keyVaultService;
        _jsRuntime = jSRuntime;
        _navigationManager = navigationManager;



        _chatService = new ChatService(this, configService);

        CurrentWorkItem = new WorkItemChat();

        // Load user
        _ = LoadUser();
    }


    /// <summary>
    /// Loads the user data from the database
    /// </summary>
    /// <returns></returns>
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

        // TODO: Remove when done testing
        EnableFileUpload = await _authUserService.TestInRole(["TestUser"]);
        if (Name == "Øystein Tveito Test")
        {
            EnableFileUpload = true;
        }


        if (_configuration.GetValue<bool>("DBSettings:UseEncryption", defaultValue: false))
        {
            User.AesKey = await _keyVaultService.GetKeyAsync(username);
        }
        
        User.Preferences = await _databaseService.GetUserPreferences(username);
        var workItems = await _databaseService.GetWorkItemList(User);
        User.Chats = workItems.OfType<WorkItemChat>().ToList();
        Loading = false;
        NewChat();
        RaiseUpdate();
    }


    /// <summary>
    /// Raises an update on all components that is subscribed to the event
    /// </summary>
    public void RaiseUpdate()
    {
        OnUpdate?.Invoke();
    }

    public void NewChat()
    {
        if (CurrentChat.Messages.Count == 0)
        {
            // TODO: Implement
            // Destroy the object?
        }

        CurrentWorkItem = new WorkItemChat();
        CurrentWorkItem.Persistant = User.Preferences.SaveHistory;
        CurrentChat.Settings.Copy(User.Preferences.DefaultChatSettings);
        RaiseUpdate();

        if (_navigationManager.Uri != _navigationManager.BaseUri)
        {
            // TODO: When done testing, uncomment
            //_navigationManager.NavigateTo("/");
        }
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

        RaiseUpdate();
    }

    public async Task SetDefaultChatSettings()
    {
        User.Preferences.DefaultChatSettings.Copy(CurrentChat.Settings);
        User.Preferences.SaveHistory = CurrentChat.Persistant;
        
        await _databaseService.SaveUserPreferences(User.Username, User.Preferences);

        RaiseUpdate();

    }

    public List<Model> GetModelList()
    {
        return _configService.GetModels();
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
        var model = _configService.GetModel(chat.Settings.Model);
        return model.MaxTokens;
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
        await _databaseService.SaveWorkItem(User, workItem);
        RaiseUpdate();
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

        await _databaseService.DeleteWorkItem(User, workItem);
        RaiseUpdate();
    }

    public async Task DeleteUser()
    {
        await _databaseService.DeleteUser(User.Username);
        User = new User();
        await LoadUser();
        RaiseUpdate();
    }

    public async Task SendMessage()
    {
        await SendMessage(null);
    }

    public async Task SendMessage(string? message)
    {
        Waiting = true;
        if (!User.Chats.Contains(CurrentChat))
        {
            User.Chats.Add(CurrentChat);
            RaiseUpdate();
            await UpdateWorkItem(CurrentChat);
        }
        await _chatService.GetChatResponse(message);
        Waiting = false;
        RaiseUpdate();
    }

    public async Task RegerateFromIndex(int index)
    {
        var chat = CurrentChat;
        Waiting = true;
        List<Task> tasks = new List<Task>();
        for (int i = chat.Messages.Count -1; i > index; i--)
        {
            var message = chat.Messages[i];
            tasks.Add(_databaseService.DeleteChatMessage(message));
            chat.Messages.Remove(message);
        }
        await Task.WhenAll(tasks);

        await SendMessage(null);
    }

    public void StreamUpdated()
    {
        RaiseUpdate();
        _jsRuntime.InvokeVoidAsync("forceScroll", "chatContainer");
        ScrollDelayed();
    }

    private async void ScrollDelayed()
    {
        await Task.Delay(200);
        await _jsRuntime.InvokeVoidAsync("forceScroll", "chatContainer");
    }


}
