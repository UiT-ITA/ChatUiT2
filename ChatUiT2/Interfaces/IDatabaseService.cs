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
    Task SaveWorkItem(User user, IWorkItem workItem);
    Task DeleteWorkItem(User user, IWorkItem workItem);

    // ChatMessages
    Task SaveChatMessages(User user, WorkItemChat chat);
    Task DeleteChatMessage(ChatMessage message);
    Task DeleteMissingMessages(User user, WorkItemChat chat);
}
