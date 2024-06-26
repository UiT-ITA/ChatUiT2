using ChatUiT2.Models;

namespace ChatUiT2.Interfaces;

public interface IUserService
{
    IWorkItem CurrentWorkItem { get; set; }
    bool IsDarkMode { get; set; }
    bool UseMarkdown { get; set; }
    bool SmoothOutput { get; set; }
    bool Waiting { get; set; }
    bool Loading { get; set;  }
    string Name { get; set; }
    bool IsAdmin { get; set; }
    bool EnableFileUpload { get; set; }
    IConfigService _configService { get; set; }
    WorkItemChat CurrentChat { get; }
    ChatWidth ChatWidth { get; set; }

    void NewChat();
    void SetWorkItem(IWorkItem workItem);
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
    Task DeleteUser();
    Task SendMessage(string message);
    Task SendMessage(string message, List<ChatFile> files);
    Task SendMessage();
    Task RegerateFromIndex(int index);
    void StreamUpdated();
}
