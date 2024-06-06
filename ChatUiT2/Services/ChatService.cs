using ChatUiT2.Models;
using ChatUiT2.Interfaces;
using MongoDB.Driver.Core.WireProtocol.Messages;
using MudBlazor;
using System.Text.Json;

namespace ChatUiT2.Services;

public class ChatService : IChatService
{
    private IUserService _userService { get; set; }
    private IConfigService _configService { get; set; }

    public ChatService(IUserService userService, IConfigService configService)
    {
        _userService = userService;
        _configService = configService;
    }

    
    public async Task GetChatResponse(string? message)
    {
        await GetChatResponse(_userService.CurrentChat, message);
    }

    public async Task GetChatResponse(WorkItemChat chat, string? message)
    {
        if (message != null)
        {
            chat.Messages.Add(new ChatMessage
            {
                Content = message,
                Role = ChatMessageRole.User,
                Status = ChatMessageStatus.Done
            });
        }

        ChatMessage responseMessage = new ChatMessage { Content = "", Role = ChatMessageRole.Assistant, Status = ChatMessageStatus.Working };
        chat.Messages.Add(responseMessage);

        _userService.UpdateItem(chat);
        _userService.RaiseUpdate();

        Model model = _configService.GetModel(chat.Settings.Model);
        ModelEndpoint endpoint = _configService.GetEndpoint(model.Deployment);

        Console.WriteLine("Debug: GetChatResponse");
        Console.WriteLine(model.DeploymentName);
        Console.WriteLine(endpoint.Url);
        
        if (model.DeploymentType == "AzureOpenAI")
        {
            try
            {
                var response = AzureOpenAIService.GetStreamingResponse(chat, model, endpoint);
                await foreach (var chatUpdate in response)
                {
                    responseMessage.Content += chatUpdate.ContentUpdate;

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
        else
        {
            throw new Exception("Unsupported deployment type: " + model.DeploymentType);
        }     

    }
}


