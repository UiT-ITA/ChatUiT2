using ChatUiT2.Models;

namespace ChatUiT2.Interfaces;

public interface IStorageService
{
    Task<ChatFile> GetFile(string workItemId, string filename);
    Task UploadFile(IWorkItem workItem, ChatFile file);
    Task DeleteFile(IWorkItem workItem, string filename);
    Task<IEnumerable<string>> ListChatFiles(IWorkItem workItem);
    Task DeleteContainer(IWorkItem workItem);
}
