﻿using ChatUiT2.Models;
using ChatUiT2.Interfaces;
using ChatUiT2.Tools;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using System.Text.Json;
using Azure.AI.OpenAI.Chat;
//using OpenAI.Chat;

namespace ChatUiT2.Services;

public class ChatService : IChatService
{
    private IUserService _userService { get; set; }
    private IConfigService _configService { get; set; }
    private ILogger _logger { get; set; }

    public ChatService(IUserService userService, IConfigService configService, ILogger logger)
    {
        _userService = userService;
        _configService = configService;
        _logger = logger;


        //Console.WriteLine("ChatService created");
    }

    public async Task GetChatResponse(WorkItemChat chat)
    {
        ChatMessage userMessage = chat.Messages.Last();

        ChatMessage responseMessage = new ChatMessage { Content = "", Role = ChatMessageRole.Assistant, Status = ChatMessageStatus.Working };
        if (responseMessage.Created <= userMessage.Created)
        {
            responseMessage.Created = userMessage.Created.AddMilliseconds(1);
        }

        chat.Messages.Add(responseMessage);
        _userService.StreamUpdated();

        chat.Updated = DateTimeTools.GetTimestamp();
        await _userService.UpdateWorkItem(chat);

        Model model = _configService.GetModel(chat.Settings.Model);
        ModelEndpoint endpoint = _configService.GetEndpoint(model.Deployment);


        _logger.LogInformation("Type: {LogType} User: {User} WorkItem {ChatId} Model: {Model}",
            "ChatRequest",
            _userService.UserName,
            chat.Id,
            model.Name);


        if (model.DeploymentType == "AzureOpenAI")
        {
            try
            {
                int inputTokens = AzureOpenAIService.GetChatTokens(chat, model);
                var response = AzureOpenAIService.GetStreamingResponse(chat, model, endpoint, allowFiles: true);
                await foreach (var chatUpdates in response)
                {
                    foreach (var update in chatUpdates.ContentUpdate)
                    {
                        responseMessage.Content += update.Text;
                        if (_userService.SmoothOutput)
                        {
                            await Task.Delay(20);
                            _userService.StreamUpdated();
                        }
                    }
                    _userService.StreamUpdated();


                    var finishReason = chatUpdates.FinishReason;
                    if (finishReason != null)
                    {
                        //Console.WriteLine(chatUpdates.Usage);

                        int outputTokens = AzureOpenAIService.GetTokens(model.DeploymentName, responseMessage.Content);

                        _logger.LogInformation("Type: {LogType}, User: {User}, Model: {Model} Input: {Input}, Output: {Output}", 
                            "ChatRequest", _userService.UserName, model.DeploymentName, inputTokens, outputTokens);

                        switch (finishReason.Value.ToString())
                        {
                            case "Stop":
                                responseMessage.Status = ChatMessageStatus.Done;
                                break;
                            case "Length":
                                // TODO: Handle continue option
                                responseMessage.Status = ChatMessageStatus.TokenLimit;
                                break;
                            default:
                                responseMessage.Status = ChatMessageStatus.Error;
                                break;
                        }
                    }
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

                    //_userService.RaiseUpdate();
                    _userService.StreamUpdated();
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

        if (chat.Name == "New chat")
        {


            chat.Name = await GetName(chat);
            _userService.StreamUpdated();

        }

        chat.Updated = DateTimeTools.GetTimestamp();
        await _userService.UpdateWorkItem(chat);

    }

    public async Task<string> GetName(WorkItemChat chat)
    {
        string name;
        var model = _configService.GetNamingModel();
        var endpoint = _configService.GetEndpoint(model.Deployment);

        string namingPrompt = "You are a naming service. Name the chat bellow. ONLY reply with the name. The name should be a maximum of 25 characters long. The name will be displayed on a label, so make it as informative as you can. Do NOT put quotation mark around your answer. Reply ONLY with the name. Do NOT format the answer in any way. Do not refer back to this prompt in any way. The name should have nothing to do with this specific prompt.";

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
            name =  await AzureOpenAIService.GetResponse(namingChat, model, endpoint);
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

        if (string.IsNullOrEmpty(name))
        {
            name = "New chat";
        }

        return name;
    }
}


