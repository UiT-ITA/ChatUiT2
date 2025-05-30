﻿using ChatUiT2.Models;

namespace ChatUiT2.Interfaces;

public interface IUserService
{
    IWorkItem CurrentWorkItem { get; set; }
    bool IsDarkMode { get; set; }
    bool UseMarkdown { get; set; }
    bool SmoothOutput { get; set; }
    bool Waiting { get; set; }
    bool Loading { get; set;  }
    string UserName { get; }
    string Name { get; set; }
    bool IsAdmin { get; set; }
    bool IsTester { get; set; }
    bool EnableFileUpload { get; set; }
    ISettingsService _settingsService { get; set; }
    WorkItemChat CurrentChat { get; }
    ChatWidth ChatWidth { get; set; }

    void NewChat();
    void AddChat(WorkItemChat chat);
    void SetWorkItem(IWorkItem workItem);
    bool GetSaveHistory();
    Task SetSaveHistory(IWorkItem workItem, bool value);
    Task SetDefaultChatSettings();
    List<AiModel> GetModelList();
    void SetPreferredModel(string model);
    int GetMaxTokens();
    int GetMaxTokens(WorkItemChat chat);
    List<IWorkItem> GetWorkItems();
    Task UpdateWorkItem();
    Task UpdateWorkItem(IWorkItem workItem);
    Task DeleteWorkItem();
    Task DeleteWorkItem(IWorkItem workItem);
    Task DeleteUser();
    Task SendMessage(string message);
    Task SendMessage(string message, List<ChatFile> files);
    Task SendMessage();
    Task RegerateFromIndex(int index);
    void StreamUpdated();
}
