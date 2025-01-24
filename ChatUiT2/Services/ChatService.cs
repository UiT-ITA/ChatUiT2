using Azure.AI.OpenAI;
using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using ChatUiT2.Tools;
using OpenAI.Chat;
using System.Buffers;
using System.ClientModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tiktoken;
using Tiktoken.Encodings;
using ZstdSharp.Unsafe;

namespace ChatUiT2.Services;

public class ChatService : IChatService
{
    private IUserService _userService { get; set; }
    private ISettingsService _settingsService { get; set; }
    private ILogger _logger { get; set; }

    public ChatService(IUserService userService, ISettingsService settingsService, ILogger logger)
    {
        _userService = userService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task GetChatResponse(WorkItemChat chat)
    {
        Models.ChatMessage userMessage = chat.Messages.Last();

        Models.ChatMessage responseMessage = new Models.ChatMessage
        {
            Content = "",
            Role = Models.ChatMessageRole.Assistant,
            Status = ChatMessageStatus.Working
        };

        if (responseMessage.Created <= userMessage.Created)
        {
            responseMessage.Created = userMessage.Created.AddMilliseconds(1);
        }

        chat.Messages.Add(responseMessage);
        _userService.StreamUpdated();

        chat.Updated = DateTimeTools.GetTimestamp();
        await _userService.UpdateWorkItem(chat);

        AiModel model = _settingsService.GetModel(chat.Settings.Model);

        //_logger.LogInformation("Type: {LogType} User: {User} WorkItem {ChatId} Model: {Model}",
        //    "ChatRequest",
        //    _userService.UserName,
        //    chat.Id,
        //    model.Name);

        if (model.DeploymentType == DeploymentType.AzureOpenAI)
        {
            await HandleAzureOpenai(chat, model, responseMessage);
        }
        else
        {
            throw new ArgumentException("Model is not an AzureOpenAI model");
        }

        if (chat.Name == "New chat")
        {


            chat.Name = await GetName(chat);
            _userService.StreamUpdated();

        }

        chat.Updated = DateTimeTools.GetTimestamp();
        await _userService.UpdateWorkItem(chat);
    }

    private async Task HandleAzureOpenai(WorkItemChat chat, AiModel model, Models.ChatMessage responseMessage)
    {
        try
        {
            var openAIService = new OpenAIService(model, _userService);

            int inputTokens = openAIService.GetTokens(chat);
            await openAIService.GetStreamingResponse(chat, responseMessage);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling AzureOpenAI");
        }
    }

    public async Task<string> GetName(WorkItemChat chat)
    {
        string name;
        var model = _settingsService.NamingModel;

        string namingPrompt = "You are a naming service. Name the chat bellow. ONLY reply with the name. The name should be a maximum of 25 characters long. The name will be displayed on a label, so make it as informative as you can. Do NOT put quotation mark around your answer. Reply ONLY with the name. Do NOT format the answer in any way. Do not refer back to this prompt in any way. The name should have nothing to do with this specific prompt.";

        WorkItemChat namingChat = new WorkItemChat
        {
            Settings = new ChatSettings
            {
                Model = model.DisplayName,
                Prompt = namingPrompt,
                MaxTokens = 20,
                Temperature = 0.7f
            },
            Messages = chat.Messages
        };

        if (model.DeploymentType == DeploymentType.AzureOpenAI)
        {
            var openAIService = new OpenAIService(model, _userService);
            
            name = await openAIService.GetResponse(namingChat);
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

public class OpenAIService
{
    private AiModel _model { get; set; }
    private IUserService _userService { get; set; }
    private List<ChatTool> _tools { get; set; } = new List<ChatTool>();
    private ChatClient _client { get; set; }
    public OpenAIService(AiModel model, IUserService userService)
    {
        if (model.DeploymentType != DeploymentType.AzureOpenAI)
        {
            throw new ArgumentException("Model is not an AzureOpenAI model");
        }
        _client = new AzureOpenAIClient(new Uri(model.Endpoint), new ApiKeyCredential(model.ApiKey)).GetChatClient(model.DeploymentName);
        _model = model;
        _userService = userService;
    }

    public async Task<string> GetResponse(WorkItemChat chat, bool allowFiles = false)
    {
        var messages = GetOpenAiMessages(chat, _model.MaxContext - chat.Settings.MaxTokens , allowFiles);

        var options = new ChatCompletionOptions()
        {
            MaxOutputTokenCount = Math.Min(_model.MaxTokens, chat.Settings.MaxTokens),
            Temperature = chat.Settings.Temperature,
        };

        ChatCompletion response = await _client.CompleteChatAsync(messages, options);

        return response.Content[0].Text;
    }


    public async Task GetStreamingResponse(WorkItemChat chat, Models.ChatMessage responseMessage, bool allowFiles = true)
    {

        var options = new ChatCompletionOptions()
        {
            MaxOutputTokenCount = Math.Min(_model.MaxTokens, chat.Settings.MaxTokens),
            Temperature = chat.Settings.Temperature,
        };

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


        int availableTokens = _model.MaxContext - (int)options.MaxOutputTokenCount;
        List<OpenAI.Chat.ChatMessage> messages = GetOpenAiMessages(chat, availableTokens, allowFiles);


        await CompleteChatStreamingAsync(messages, options, responseMessage);

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
                _userService.StreamUpdated();
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
                _userService.StreamUpdated();

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


                    messages.Add(new ToolChatMessage(toolCall.Id, await ChatTools.HandleToolCall(toolCall)));
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


    public List<OpenAI.Chat.ChatMessage> GetOpenAiMessages(WorkItemChat chat, int maxTokens, bool allowFiles = true)
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
            if (allowFiles)
            {
                foreach (var file in message.Files)
                {
                    fileTokens += GetTokens(file);
                }
            }

            if (messageTokens + fileTokens > availableTokens)
            {
                break;
            }

            if (message.Content == string.Empty)
            {
                if (!allowFiles || message.Files.Count == 0)
                {
                    continue;
                }
            }

            OpenAI.Chat.ChatMessage requestMessage;
            if (allowFiles)
            {
                if (message.Files.Count > 0)
                {
                    messages.Insert(1, GetOpenAIMessage(message));
                    //Console.WriteLine(message.Content);
                    foreach (var file in message.Files)
                    {

                        messages.Insert(1, FileTools.GetOpenAIMessage(file));
                    }
                    requestMessage = new UserChatMessage("Here is a list of files:\n");
                }
                else
                {
                    requestMessage = GetOpenAIMessage(message);
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
        if (_model.ModelName == ModelName.gpt4o || _model.ModelName == ModelName.gpt4omini)
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
        if (_model.ModelName == ModelName.gpt4o || _model.ModelName == ModelName.gpt4omini)
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
                if (_model.ModelName == ModelName.gpt4o)
                {
                    imageTokens = 85 + 170 * (int)Math.Ceiling(imgPart.Width / 512.0) * (int)Math.Ceiling(imgPart.Height / 512.0);
                }
                else if (_model.ModelName == ModelName.gpt4omini)
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

}




// Copied from openai
public class StreamingChatToolCallsBuilder
{
    private readonly Dictionary<int, string> _indexToToolCallId = [];
    private readonly Dictionary<int, string> _indexToFunctionName = [];
    private readonly Dictionary<int, SequenceBuilder<byte>> _indexToFunctionArguments = [];

    public void Append(StreamingChatToolCallUpdate toolCallUpdate)
    {
        // Keep track of which tool call ID belongs to this update index.
        if (toolCallUpdate.ToolCallId != null)
        {
            _indexToToolCallId[toolCallUpdate.Index] = toolCallUpdate.ToolCallId;
        }

        // Keep track of which function name belongs to this update index.
        if (toolCallUpdate.FunctionName != null)
        {
            _indexToFunctionName[toolCallUpdate.Index] = toolCallUpdate.FunctionName;
        }

        // Keep track of which function arguments belong to this update index,
        // and accumulate the arguments as new updates arrive.
        if (toolCallUpdate.FunctionArgumentsUpdate != null && !toolCallUpdate.FunctionArgumentsUpdate.ToMemory().IsEmpty)
        {
            if (!_indexToFunctionArguments.TryGetValue(toolCallUpdate.Index, out SequenceBuilder<byte>? argumentsBuilder))
            {
                argumentsBuilder = new SequenceBuilder<byte>();
                _indexToFunctionArguments[toolCallUpdate.Index] = argumentsBuilder;
            }

            argumentsBuilder.Append(toolCallUpdate.FunctionArgumentsUpdate);
        }
    }

    public IReadOnlyList<ChatToolCall> Build()
    {
        List<ChatToolCall> toolCalls = [];

        foreach (KeyValuePair<int, string> indexToToolCallIdPair in _indexToToolCallId)
        {
            ReadOnlySequence<byte> sequence = _indexToFunctionArguments[indexToToolCallIdPair.Key].Build();

            ChatToolCall toolCall = ChatToolCall.CreateFunctionToolCall(
                id: indexToToolCallIdPair.Value,
                functionName: _indexToFunctionName[indexToToolCallIdPair.Key],
                functionArguments: BinaryData.FromBytes(sequence.ToArray()));

            toolCalls.Add(toolCall);
        }

        return toolCalls;
    }
}
public class SequenceBuilder<T>
{
    private Segment? _first;
    private Segment? _last;

    public void Append(ReadOnlyMemory<T> data)
    {
        if (_first == null)
        {
            Debug.Assert(_last == null);
            _first = new Segment(data);
            _last = _first;
        }
        else
        {
            _last = _last!.Append(data);
        }
    }

    public ReadOnlySequence<T> Build()
    {
        if (_first == null)
        {
            Debug.Assert(_last == null);
            return ReadOnlySequence<T>.Empty;
        }

        if (_first == _last)
        {
            Debug.Assert(_first.Next == null);
            return new ReadOnlySequence<T>(_first.Memory);
        }

        return new ReadOnlySequence<T>(_first, 0, _last!, _last!.Memory.Length);
    }

    private sealed class Segment : ReadOnlySequenceSegment<T>
    {
        public Segment(ReadOnlyMemory<T> items) : this(items, 0)
        {
        }

        private Segment(ReadOnlyMemory<T> items, long runningIndex)
        {
            Debug.Assert(runningIndex >= 0);
            Memory = items;
            RunningIndex = runningIndex;
        }

        public Segment Append(ReadOnlyMemory<T> items)
        {
            long runningIndex;
            checked { runningIndex = RunningIndex + Memory.Length; }
            Segment segment = new(items, runningIndex);
            Next = segment;
            return segment;
        }
    }
}