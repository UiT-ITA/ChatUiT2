using ChatUiT2.Interfaces;
using ChatUiT2.Models;

namespace ChatUiT2.Services;

public class AssistantService : IAssistantService
{
    private readonly List<Assistant> _assistants = new List<Assistant>();
    private readonly IUserService _userService;
    private readonly ISettingsService _settingsService;

    public AssistantService(IUserService userService, ISettingsService settingsService)
    {
        _userService = userService;
        _settingsService = settingsService;
        _assistants = new List<Models.Assistant>
        {
            new Assistant
            {
                Name = "GPT-4o",
                Description = "Fast and good model",
                Model = settingsService.GetModel("GPT-4o"),
                SystemPrompt = "You are a helpful assistant",
                Owner = "System",
                AllowedRoles = new List<UserRole> { UserRole.User }
            },
            new Assistant
            {
                Name = "GPT-4o-Mini",
                Description = "Fast model",
                Model = settingsService.GetModel("GPT-4o-Mini"),
                SystemPrompt = "You are a helpful assistant",
                Owner = "System",
                AllowedRoles = new List<UserRole> { UserRole.User, UserRole.External }
            },
            new Assistant
            {
                Name = "GPT-4o-Test",
                Description = "Fast test model",
                Model = settingsService.GetModel("GPT-4o-Mini"),
                SystemPrompt = "You are a helpful assistant",
                Owner = "System",
                AllowedRoles = new List<UserRole> { UserRole.BetaTester }
            },
            new Assistant
            {
                Name = "Proofreader",
                Description = "Proofreads supplied texts and gives feedback",
                Model = settingsService.GetModel("GPT-4o-Mini"),
                SystemPrompt = "You are ProofreadPro, a meticulous and helpful AI assistant specializing in proofreading and language enhancement assistance. Your primary goal is to review and refine texts by identifying and correcting:\n\n- **Grammatical Errors:** Check for errors in verb tense, subject–verb agreement, sentence structure, etc.\n- **Spelling Mistakes:** Detect and correct typos or unusual word choices.\n- **Punctuation Issues:** Ensure the appropriate use of commas, periods, semicolons, and other punctuation marks to maintain clarity.\n- **Formatting and Consistency:** Verify consistency in style, capitalization, and document formatting.\n- **Clarity and Readability:** Suggest improvements that preserve the original voice and intended meaning while enhancing overall readability.\n\nWhen processing any text:\n- **Preserve the Author’s Voice:** Make improvements without altering the stylistic tone unless explicitly requested.\n- **Explain Your Corrections:** When making corrections or suggestions, provide brief, clear explanations to help the user understand the changes.\n- **Ask Clarifying Questions:** If the context or intent of a passage is ambiguous, respectfully ask clarifying questions before proceeding.\n- **Provide Multiple Options When Appropriate:** If different corrections or stylistic choices are valid, outline alternatives and their potential impacts.\n\nAlways maintain a respectful, concise, and constructive tone to help users improve their writing while keeping the original meaning intact.",
                Owner = "otv001@uit.no",
                VisiblePrompt = false
            },
            new Assistant
            {
                Name = "Markos amazing model",
                Description = "Does amazing things",
                Model = settingsService.GetModel("GPT-4o-Mini"),
                SystemPrompt = "You are a helpful assistant",
                Owner = "Marko",
                AllowedUsers = new List<string> { "otv001@uit.no" }
            },
            new Assistant
            {
                Name = "Markos second amazing model",
                Description = "Does amazing things",
                Model = settingsService.GetModel("GPT-4o-Mini"),
                SystemPrompt = "You are a helpful assistant",
                Owner = "Marko",
                AllowedUsers = new List<string> {  }
            }
        };
    }

    public Task CreateAssistant(Assistant assistant)
    {
        assistant.Owner = _userService.UserName;
        _assistants.Add(assistant);
        return Task.CompletedTask;
    }

    public Task UpdateAssistant(Assistant assistant)
    {
        var index = _assistants.FindIndex(a => a.Id == assistant.Id);
        if (index >= 0)
        {
            if (_assistants[index].Owner != _userService.UserName || assistant.Owner != _userService.UserName)
            {
                Console.WriteLine("User is not owner of assistant");
                return Task.CompletedTask;
            }
            _assistants[index] = assistant;
        }

        Console.WriteLine("Updated assistant: " + assistant.Name);
        Console.WriteLine("UPDATE IN DATABASE!");

        return Task.CompletedTask;
    }

    public Task DeleteAssistant(string id)
    {
        var assistant = _assistants.FirstOrDefault(a => a.Id == id);
        if (assistant != null && assistant.Owner != _userService.UserName)
        {
            Console.WriteLine("User is not owner of assistant");
            return Task.CompletedTask;
        }
        if (assistant != null)
        {
            _assistants.Remove(assistant);
            Console.WriteLine("Deleted assistant: " + assistant.Name);
            Console.WriteLine("UPDATE IN DATABASE!");
        }
        return Task.CompletedTask;
    }

    public Task DeleteAssistant(Assistant assistant)
    {
        throw new NotImplementedException();
    }

    public Task<List<Assistant>> GetAllAssistants()
    {
        return Task.FromResult(_assistants);
    }

    public Task<List<Assistant>> GetUserAssistants()
    {
        var result = _assistants
            .Where(a => a.Owner == _userService.UserName)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<Assistant>> GetSystemAssistants()
    {
        var result = _assistants
            .Where(a => a.Owner == "System" && a.AllowedRoles.Any(role => _userService.IsInRole(role)))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<Assistant>> GetSharedAssistants()
    {
        var result = _assistants
            .Where(a => a.Owner != _userService.UserName && a.AllowedUsers.Contains(_userService.UserName))
            .ToList();
        return Task.FromResult(result);
    }
}
