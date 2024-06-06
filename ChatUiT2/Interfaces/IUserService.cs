using ChatUiT2.Models;
using ChatUiT2.Services;
using ChatUiT2.Tools;

namespace ChatUiT2.Interfaces;

public interface IUserService
{
    IWorkItem CurrentWorkItem { get; set; }
    bool IsDarkMode { get; set; }
    bool Waiting { get; set; }
    IConfigService _configService { get; set; }
    //IAuthUserService _authUserService { get; set; }
    WorkItemChat CurrentChat { get; }
    event Action? OnUpdate;
    void RaiseUpdate();
    void NewChat();
    bool GetSaveHistory();
    Task SetSaveHistory(IWorkItem workItem, bool value);
    Task SetDefaultChatSettings();
    List<Model> GetModelList();
    void SetPreferredModel(string model);
    int GetMaxTokens();
    int GetMaxTokens(WorkItemChat chat);
    List<IWorkItem> GetWorkItems();
    Task UpdateWorkItem();
    Task UpdateWorkItem(IWorkItem workItem);
    Task DeleteWorkItem();
    Task DeleteWorkItem(IWorkItem workItem);
    Task SendMessage();
    Task SendMessage(string? message);
    Task UpdateItem(IWorkItem workItem);
    
}
