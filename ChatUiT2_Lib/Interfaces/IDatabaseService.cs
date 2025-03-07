using ChatUiT2.Models;

namespace ChatUiT2.Interfaces;

public interface IDatabaseService
{
    // Users
    Task<Preferences> GetUserPreferences(string username);
    Task SaveUserPreferences(string username, Preferences preferences);
    Task DeleteUser(string username);

    // WorkItems
    Task<List<IWorkItem>> GetWorkItemList(User user);
    Task<List<IWorkItem>> GetWorkItemListLazy(User user, IUpdateService updateService);
    Task LoadWorkItemComponentsAsync(User user, WorkItemChat workItem, IUpdateService updateService);
    Task SaveWorkItem(User user, IWorkItem workItem);
    Task DeleteWorkItem(User user, IWorkItem workItem);

    // ChatMessages
    Task SaveChatMessages(User user, WorkItemChat chat);
    Task DeleteChatMessage(ChatMessage message, WorkItemChat chat, User user);
    Task DeleteMissingMessages(User user, WorkItemChat chat);


    // Admin functions
    Task<List<User>> GetUsers();
}
