using ChatUiT2.Models;
using MongoDB.Driver.Core.WireProtocol.Messages;
using MudBlazor;
using System.Text.Json;

namespace ChatUiT2.Services;

public class ChatService
{
    private IConfiguration _configuration { get; set; }
    private UserService _userService { get; set; }

    private List<Model> _models { get; set; }

    private AzureOpenAIService _azureOpenAIService { get; set; }
    public ChatService(IConfiguration configuration, UserService userService)
    {
        _configuration = configuration;
        _userService = userService;

        string? endpoint = _configuration["AzureOpenAI:Endpoint"];
        string? key = _configuration["AzureOpenAI:Key"];

        if (endpoint == null || key == null)
        {
            throw new Exception("Endpoint configuration missing!");
        }

        _azureOpenAIService = new AzureOpenAIService(new AzureEndpointConfig
        {
            Endpoint = endpoint,
            Key = key
        });

        ImportModels();

    }

    private void ImportModels()
    {
        var modelsSection = _configuration.GetSection("Models");
        _models = modelsSection.Get<List<Model>>() ?? new List<Model>();

    }

    public Model GetModel(string name)
    {
        Model defaultModel = _models[0];
        Model? model = _models.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (model == null)
        {
            Console.WriteLine("Invalid model selected!");
            model = _models[0];
        }
        return model;
    }

    public List<Model> GetModels()
    {
        return _models;
    }

    public async Task GetResponse(string? message)
    {
        WorkItemChat chat = _userService.CurrentChat;

        if (message != null)
        {
            chat.Messages.Add(new ChatMessage
            {
                Content = message,
                Role = ChatMessageRole.User,
                Status = ChatMessageStatus.Done
            });
        }

        ChatMessage responseMessage = new ChatMessage();
        responseMessage.Role = ChatMessageRole.Assistant;
        responseMessage.Status = ChatMessageStatus.Working;
        chat.Messages.Add(responseMessage);

        _userService.UpdateItem(chat);


        ChatRequest chatRequest = new ChatRequest
        {
            Chat = chat,
            Model = GetModel(chat.Settings.Model)
        };

        Console.WriteLine(chatRequest.Model.Name);

        


        try
        {
            var response = _azureOpenAIService.GetStreamingResponse(chatRequest);
            await foreach (var chatUpdate in response)
            {
                responseMessage.Content += chatUpdate.ContentUpdate;

                Console.Write(chatUpdate.ContentUpdate);

                // TODO: update user interface
                _userService.RaiseUpdate();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
            Console.WriteLine("Error: " + ex.StackTrace);
            responseMessage.Content = "Something went wrong...";
            responseMessage.Status = ChatMessageStatus.Error;
        }

    }
}

public class ChatRequest
{
    public WorkItemChat Chat { get; set; } = null!;
    public Model Model { get; set; } = null!;
}


