﻿using Azure.AI.OpenAI;
using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using ChatUiT2.Models.Mediatr;
using ChatUiT2.Tools;
using MediatR;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using OpenAI.Embeddings;
using System.ClientModel;
using System.Text;
using System.Text.Json;
using Tiktoken.Encodings;

namespace ChatUiT2.Services;

public class OpenAIService : IOpenAIService
{
    private readonly IMediator _mediator;
    private readonly IChatToolsService _chatToolsService;

    private AiModel _model { get; set; }
    private string _username { get; set; }
    private ILogger _logger { get; set; }
    private List<ChatTool> _tools { get; set; } = new List<ChatTool>();
    private ChatClient _client { get; set; }
    private AzureOpenAIClient _azureOpenAiClient { get; set; }
    private EmbeddingClient _embeddingClient { get; set; }

    public OpenAIService(AiModel model,
                         string username,
                         ILogger logger,                         
                         IMediator mediator,
                         IChatToolsService chatToolsService)
    {
        if (model.DeploymentType != DeploymentType.AzureOpenAI)
        {
            throw new ArgumentException("Model is not an AzureOpenAI model");
        }
        _azureOpenAiClient = new AzureOpenAIClient(new Uri(model.Endpoint), new ApiKeyCredential(model.ApiKey));
        _client = _azureOpenAiClient.GetChatClient(model.DeploymentName);
        _embeddingClient = _azureOpenAiClient.GetEmbeddingClient(model.DeploymentName);
        _model = model;
        _username = username;
        _logger = logger;
        this._mediator = mediator;
        this._chatToolsService = chatToolsService;
    }

    public async Task<string> GetResponse(WorkItemChat chat, bool allowFiles = false)
    {
        var messages = GetOpenAiMessages(chat, _model.MaxContext - chat.Settings.MaxTokens, allowFiles);

        var options = new ChatCompletionOptions()
        {
            MaxOutputTokenCount = Math.Min(_model.MaxTokens, chat.Settings.MaxTokens),
            Temperature = chat.Settings.Temperature,
        };

        ChatCompletion response = await _client.CompleteChatAsync(messages, options);

        return response.Content[0].Text;
    }


    public async Task GetStreamingResponse(WorkItemChat chat, Models.ChatMessage responseMessage, bool allowImages = true)
    {
        var options = new ChatCompletionOptions();

        int maxTokens = _model.MaxTokens;

        if (_model.Capabilities.ReasoningEffortLevel is not null)
        {
            options.ReasoningEffortLevel = _model.Capabilities.ReasoningEffortLevel;
        }
        else
        {
            maxTokens = _model.MaxTokens;
            options.MaxOutputTokenCount = maxTokens;
            options.Temperature = chat.Settings.Temperature;
        }

        foreach (var tool in _model.RequiredTools)
        {
            options.Tools.Add(tool.Tool);
        }

        foreach (var tool in _model.OptionalTools)
        {
            if (tool.Selected && !options.Tools.Contains(tool.Tool))
            {
                options.Tools.Add(tool.Tool);
            }
        }


        int availableTokens = _model.MaxContext - maxTokens;
        List<OpenAI.Chat.ChatMessage> messages = GetOpenAiMessages(chat, availableTokens, allowImages);

        int inputTokens = GetTokens(chat);

        await CompleteChatStreamingAsync(messages, options, responseMessage);

        int outputTokens = GetTokens(responseMessage.Content);

        _logger.LogInformation("Type: {LogType}, User: {User}, Model: {Model} Input: {Input}, Output: {Output}",
                               "ChatRequest",
                               _username,
                               _model.DeploymentName,
                               inputTokens,
                               outputTokens);



    }


    private async Task CompleteChatStreamingAsync(List<OpenAI.Chat.ChatMessage> messages, ChatCompletionOptions options, Models.ChatMessage responseMessage)
    {
        var response = _client.CompleteChatStreamingAsync(messages, options);

        StreamingChatToolCallsBuilder toolCalls = new();
        StringBuilder contentBuilder = new();

        await foreach (var chatCompletionUpdate in response)
        {
            foreach (var update in chatCompletionUpdate.ContentUpdate)
            {
                responseMessage.Content += update.Text;
                contentBuilder.Append(update.Text);
                await _mediator.Publish(new StreamUpdatedEvent());
            }

            foreach (var update in chatCompletionUpdate.ToolCallUpdates)
            {
                toolCalls.Append(update);
            }

            if (chatCompletionUpdate.FinishReason is null)
            {
                continue;
            }

            if (chatCompletionUpdate.FinishReason == ChatFinishReason.Stop)
            {
                responseMessage.Status = ChatMessageStatus.Done;
            }
            else if (chatCompletionUpdate.FinishReason == ChatFinishReason.Length)
            {
                responseMessage.Status = ChatMessageStatus.TokenLimit;
            }
            else if (chatCompletionUpdate.FinishReason == ChatFinishReason.ToolCalls)
            {
                await _mediator.Publish(new StreamUpdatedEvent());

                var toolList = toolCalls.Build();
                var assistantMessage = new AssistantChatMessage(toolList);
                if (contentBuilder.Length > 0)
                {
                    assistantMessage.Content.Add(ChatMessageContentPart.CreateTextPart(contentBuilder.ToString()));
                    Console.WriteLine(contentBuilder.ToString());
                }

                messages.Add(assistantMessage);

                foreach (var toolCall in toolList)
                {
                    // Add notice in response message including parameters
                    // If message is empty, or last char is a newline, don't add a newline, otherwise add a newline
                    if (responseMessage.Content == "" || responseMessage.Content[^1] == '\n')
                    {
                        responseMessage.Content += GetToolNotice(toolCall);
                    }
                    else
                    {
                        responseMessage.Content += "\n" + GetToolNotice(toolCall);
                    }
                    await _mediator.Publish(new StreamUpdatedEvent());

                    ToolChatMessage toolMessage = new(toolCall.Id, await _chatToolsService.HandleToolCall(toolCall));

                    messages.Add(toolMessage);

                }
                await CompleteChatStreamingAsync(messages, options, responseMessage);

            }
            else
            {
                Console.WriteLine("Unknown finish reason");
                Console.WriteLine(chatCompletionUpdate.FinishReason.ToString());
                responseMessage.Status = ChatMessageStatus.Error;
            }
        }
    }



    private string GetToolNotice(ChatToolCall toolCall)
    {
        // Return string in the form "> 👀**functionName(argumentName1 = argument1, argumentname2 = argument2)**\n"

        StringBuilder notice = new();
        notice.Append("> 🔎**");
        notice.Append(toolCall.FunctionName);
        notice.Append("**(");
        //notice.Append(System.Text.Encoding.UTF8.GetString(toolCall.FunctionArguments.ToArray()));
        var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(toolCall.FunctionArguments.ToString());
        if (arguments != null)
        {
            bool isFirst = true;
            foreach (var argument in arguments)
            {
                if (!isFirst)
                {
                    notice.Append(", ");
                }
                notice.Append(argument.Key);
                notice.Append(" = ");
                notice.Append(argument.Value);
                isFirst = false;
            }
        }
        notice.Append(")\n\n");
        return notice.ToString();
    }


    public List<OpenAI.Chat.ChatMessage> GetOpenAiMessages(WorkItemChat chat, int maxTokens, bool allowImages = true)
    {
        List<OpenAI.Chat.ChatMessage> messages = new();

        int availableTokens = maxTokens;

        messages.Add(new SystemChatMessage(chat.Settings.Prompt));

        for (int i = chat.Messages.Count - 1; i >= 0; i--)
        {
            var message = chat.Messages[i];
            if (message.Status == ChatMessageStatus.Error) continue;
            int messageTokens = GetTokens(message.Content);

            int fileTokens = 0;
            foreach (var file in message.Files)
            {
                fileTokens += GetTokens(file);
            }

            if (messageTokens + fileTokens > availableTokens)
            {
                break;
            }

            if (message.Content == string.Empty)
            {
                if (!allowImages || message.Files.Count == 0)
                {
                    continue;
                }
            }

            OpenAI.Chat.ChatMessage requestMessage;

            if (message.Files.Count > 0)
            {
                bool filesIncluded = false;
                messages.Insert(1, GetOpenAIMessage(message));
                //Console.WriteLine(message.Content);
                foreach (var file in message.Files)
                {
                    var fileMessage = FileTools.GetOpenAIMessage(file, includeImageParts: allowImages);
                    if (fileMessage != null)
                    {
                        messages.Insert(1, fileMessage);
                        filesIncluded = true;
                    }
                }
                if (filesIncluded)
                {
                    requestMessage = new UserChatMessage("Here is a list of files:\n");
                }
                else
                {
                    requestMessage = new UserChatMessage("User tried to attach images, but you are not capable of viewing those. Advice the user to use a different model if images are important.");
                }
            }
            else
            {
                requestMessage = GetOpenAIMessage(message);
            }


            messages.Insert(1, requestMessage);
            availableTokens -= messageTokens;
        }
        return messages;
    }

    public static OpenAI.Chat.ChatMessage GetOpenAIMessage(Models.ChatMessage message)
    {
        if (message.Role == Models.ChatMessageRole.User)
        {
            return new UserChatMessage(message.Content);
        }
        else if (message.Role == Models.ChatMessageRole.Assistant)
        {
            return new AssistantChatMessage(message.Content);
        }
        else
        {
            throw new Exception("Unkown message role");
        }
    }

    public int GetTokens(WorkItemChat chat)
    {
        int tokens = 0;
        foreach (var message in chat.Messages)
        {
            tokens += GetTokens(message.Content);
            foreach (var file in message.Files)
            {
                tokens += GetTokens(file);
            }
        }
        return tokens;
    }

    public int GetTokens(string content)
    {
        // Use tiktoken to calculate tokens
        Tiktoken.Encoder encoder;
        if (_model.ModelName == ModelName.gpt_4o || _model.ModelName == ModelName.gpt_4o_mini)
        {
            encoder = new Tiktoken.Encoder(new O200KBase());
        }
        else
        {
            encoder = new Tiktoken.Encoder(new Cl100KBase());
        }
        return encoder.CountTokens(content);
    }

    public int GetTokens(ChatFile file)
    {

        // Use tiktoken to calculate tokens
        Tiktoken.Encoder encoder;
        if (_model.ModelName == ModelName.gpt_4o || _model.ModelName == ModelName.gpt_4o_mini)
        {
            encoder = new Tiktoken.Encoder(new O200KBase());
        }
        else
        {
            encoder = new Tiktoken.Encoder(new Cl100KBase());
        }

        int tokens = 0;

        foreach (ChatFilePart part in file.Parts)
        {
            if (part.Type == FilePartType.Text)
            {
                tokens += encoder.CountTokens(((TextFilePart)part).Data);
            }
            else
            {
                int imageTokens;
                ImageFilePart imgPart = (ImageFilePart)part;
                if (_model.ModelName == ModelName.gpt_4o)
                {
                    imageTokens = 85 + 170 * (int)Math.Ceiling(imgPart.Width / 512.0) * (int)Math.Ceiling(imgPart.Height / 512.0);
                }
                else if (_model.ModelName == ModelName.gpt_4o_mini)
                {
                    imageTokens = 2833 + 5667 * (int)Math.Ceiling(imgPart.Width / 512.0) * (int)Math.Ceiling(imgPart.Height / 512.0);
                }
                else
                {
                    imageTokens = 0;
                }

                tokens += imageTokens;
            }
        }

        return tokens;
    }

    public async Task<OpenAIEmbedding> GetEmbedding(string text)
    {
        var embeddingResponse = await _embeddingClient.GenerateEmbeddingAsync(text);

        return embeddingResponse;
    }
}
