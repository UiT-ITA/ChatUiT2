using ChatUiT2.Models;
using MongoDB.Driver.Core.WireProtocol.Messages;
using MudBlazor;
using System.Text.Json;

namespace ChatUiT2.Services;

public class ChatService
{
    private AppConfig _appConfig { get; set; }
    private UserService _userService { get; set; }

    public ChatService(AppConfig appConfig, UserService userService)
    {
        _appConfig = appConfig;
        _userService = userService;

    }

    
    public async Task GetChatResponse(string? message)
    {
        await GetChatResponse(message, _userService.CurrentChat);
    }

    public async Task GetChatResponse(string? message, WorkItemChat? chat)
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


