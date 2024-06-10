using ChatUiT2.Models;
using ChatUiT2.Interfaces;
using MongoDB.Driver.Core.WireProtocol.Messages;
using MudBlazor;
using System.Text.Json;
using ChatUiT2.Tools;
using System.Text.RegularExpressions;

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
            await _userService.UpdateWorkItem(chat);
        }

        ChatMessage userMessage = chat.Messages.Last();

        ChatMessage responseMessage = new ChatMessage { Content = "", Role = ChatMessageRole.Assistant, Status = ChatMessageStatus.Working };
        if (responseMessage.Created <= userMessage.Created)
        {
            responseMessage.Created = userMessage.Created.AddMilliseconds(1);
        }

        chat.Messages.Add(responseMessage);

        await _userService.UpdateWorkItem(chat);
        _userService.RaiseUpdate();

        Model model = _configService.GetModel(chat.Settings.Model);
        ModelEndpoint endpoint = _configService.GetEndpoint(model.Deployment);

        
        if (model.DeploymentType == "AzureOpenAI")
        {
            try
            {
                var response = AzureOpenAIService.GetStreamingResponse(chat, model, endpoint);
                await foreach (var chatUpdate in response)
                {
                    responseMessage.Content += chatUpdate.ContentUpdate;

                    _userService.RaiseUpdate();

                    // TODO: Handle finish reason
                    /*var finishReason = chatUpdate.FinishReason;
                    if (finishReason != null)
                    {
                        switch (finishReason.ToString())
                        {
                            case "stop":
                                responseMessage.Status = ChatMessageStatus.Done;
                                break;
                            case "length":
                                responseMessage.Status = ChatMessageStatus.TokenLimit;
                                break;
                            default:
                                responseMessage.Status = ChatMessageStatus.Error;
                                break;
                        }
                    }*/
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
        else if (model.DeploymentType == "Groq")
        {
            try
            {
                var response = GroqService.GetStreamingResponse(chat, model, endpoint);
                await foreach (var chatUpdate in response)
                {
                    //var contentUpdate = chatUpdate["choices"]?[0]?["message"]?["content"];
                    responseMessage.Content += chatUpdate?["choices"]?[0]?["delta"]?["content"];

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

        responseMessage.Created = DateTimeTools.GetTimestamp();
        await _userService.UpdateWorkItem(chat);

        if (chat.Name == "New chat")
        {


            chat.Name = await GetName(chat);
            await _userService.UpdateWorkItem(chat);
        }

    }

    public async Task<string> GetName(WorkItemChat chat)
    {
        string name;
        var model = _configService.GetNamingModel();
        var endpoint = _configService.GetEndpoint(model.Deployment);

        string namingPrompt = "Name this chat. ONLY reply with the name. The name should be a maximum of 25 characters long. The name will be displayed on a label, so make it as informative as you can. Do NOT put quotation mark around your answer. Reply ONLY with the name. Do NOT format the answer in any way";

        WorkItemChat namingChat = new WorkItemChat
        {
            Settings = new ChatSettings
            {
                Model = model.Name,
                Prompt = namingPrompt,
                MaxTokens = 20,
                Temperature = 0.7f
            },
            Messages = chat.Messages
        };

        if (model.DeploymentType == "AzureOpenAI")
        {
            name = await AzureOpenAIService.GetResponse(namingChat, model, endpoint);
        }
        else if (model.DeploymentType == "Groq")
        {
            throw new Exception("Groq naming is not yet supported");
        }
        else
        {
            throw new Exception("Unsupported deployment type: " + model.DeploymentType);
        }
        
        // Strip the name of any special characters and starting and trailing whitespaces
        name = Regex.Replace(name, @"[^\w\s]", "");

        if (name.Length > 25)
        {
            name = name.Substring(0, 25);
        }
        return name;
    }
}


