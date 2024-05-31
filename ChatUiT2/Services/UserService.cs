﻿using ChatUiT2.Models;
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






    public event Action? OnUpdate;

    public UserService(IConfiguration configuration)
    {
        _configuration = configuration;
        _chatService = new ChatService(configuration, this);

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
        //throw new NotImplementedException();
        Console.WriteLine("New chat");
    }

    public bool GetSaveHistory()
    {
        return User.Preferences.SaveHistory;
    }

    public void SetSaveHistory(bool value)
    {
        User.Preferences.SaveHistory = value;
        RaiseUpdate();
    }

    public List<Model> GetModelList()
    {
        return _chatService.GetModels();
    }

    public void SetPreferredModel(string model)
    {
        User.Preferences.DefaultChatSettings.Model = model;
    }

    public List<IWorkItem> GetWorkItems()
    {
        return User.Chats.Cast<IWorkItem>().ToList();
    }

    public void UpdateWorkItem(IWorkItem workItem)
    {
        // TODO: Implement
    }

    public void DeleteWorkItem(IWorkItem workItem)
    {
        // TODO: Implement

        Console.WriteLine("Deleting work item: " + workItem.Name);
    }


    public async Task SendMessage(string message)
    {

        await _chatService.GetResponse(message);
    }

    public async void UpdateItem(IWorkItem workItem)
    {
        workItem.Updated = DateTimeTools.GetTimestamp();
        // TODO: implement saving
        await Task.Delay(100);
        RaiseUpdate();
    }


}
