using ChatUiT2.Models;

namespace ChatUiT2.Services;

public class ChatService
{
    private IConfiguration _configuration { get; set; }
    private UserService _userService { get; set; }

    private AzureOpenAIService _azureOpenAIService { get; set; }
    public ChatService(IConfiguration configuration, UserService userService)
    {
        _configuration = configuration;
        _userService = userService;
        _azureOpenAIService = new AzureOpenAIService(configuration);


    }

    public async Task<string> GetResponse(string message)
    {


        if (((WorkItemChat)_userService.CurrentWorkItem).Settings.Model == "GPT-4-Turbo")
        {

            return await _azureOpenAIService.GetResponse();
        }

        return await Task.FromResult("Hello World");
    }
}

