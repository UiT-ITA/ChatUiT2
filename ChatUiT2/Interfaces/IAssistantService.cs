using ChatUiT2.Models;

namespace ChatUiT2.Interfaces;

public interface IAssistantService
{
    Task<List<Assistant>> GetAllAssistants();
    Task<List<Assistant>> GetSystemAssistants();
    Task<List<Assistant>> GetUserAssistants();
    Task<List<Assistant>> GetSharedAssistants();
    Task CreateAssistant(Assistant assistant);
    Task UpdateAssistant(Assistant assistant);
    Task DeleteAssistant(string id);
    Task DeleteAssistant(Assistant assistant);
}
