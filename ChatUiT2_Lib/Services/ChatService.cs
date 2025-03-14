using Azure.AI.OpenAI;
using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using ChatUiT2.Models.Mediatr;
using ChatUiT2.Tools;
using MediatR;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using System.Buffers;
using System.ClientModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tiktoken.Encodings;

namespace ChatUiT2.Services;

public class ChatService : IChatService
{
    private readonly IUsernameService _usernameService;
    private readonly IMediator _mediator;

    private ISettingsService _settingsService { get; set; }
    private ILogger _logger { get; set; }

    public ChatService(ISettingsService settingsService,
                       ILogger logger,
                       IUsernameService usernameService,
                       IMediator mediator)
    {
        _settingsService = settingsService;
        _logger = logger;
        this._usernameService = usernameService;
        this._mediator = mediator;
    }

    public async Task GetChatResponse(WorkItemChat chat)
    {
        Models.ChatMessage userMessage = chat.Messages.Last();
        Models.ChatMessage responseMessage;
        AiModel model = _settingsService.GetModel(chat.Settings.Model);

        _logger.LogInformation("Type: {LogType} User: {User} WorkItem {ChatId} Model: {Model}",
            "ChatRequest",
            await _usernameService.GetUsername(),
            chat.Id,
            model.DeploymentName);

        if (userMessage.Role == Models.ChatMessageRole.User)
        {

            responseMessage = new Models.ChatMessage
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
            chat.Updated = DateTimeTools.GetTimestamp();
            await _mediator.Publish(new UpdateWorkItemEvent { Chat = chat });
            await _mediator.Publish(new StreamUpdatedEvent());

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
                await _mediator.Publish(new StreamUpdatedEvent());

            }
        }
        else
        {
            Console.WriteLine("Continuing chat");

            responseMessage = userMessage;
            responseMessage.Status = ChatMessageStatus.Working;
            await _mediator.Publish(new StreamUpdatedEvent());

            var tempMessages = new List<Models.ChatMessage>();
            foreach (var message in chat.Messages)
            {
                tempMessages.Add(new Models.ChatMessage
                {
                    Content = message.Content,
                    Role = message.Role,
                    Status = message.Status
                });
            }

            tempMessages.Add(new Models.ChatMessage { Role = Models.ChatMessageRole.System, Content = "The last message was cut of. Coptinue the last message from *EXACTLY* where it left off. Do **NOT** introduce new formating if not needed. Do not start a new codeblock if you are not starting a new block of code. Continue with the next token in the message." });


            var tempChat = new WorkItemChat
            {
                Settings = new ChatSettings
                {
                    Model = chat.Settings.Model,
                    Prompt = chat.Settings.Prompt + "\n\nWhen continuing a message:\n1. **Continuation of Responses:**\n   - **Seamless Continuation:** When continuing from a previous message, continue directly from where the last message left off. Do not introduce new formatting or code blocks.\n   - **Mid-Code Continuation:** If the previous message was a code block that was not closed, continue writing the code without starting a new code block. Simply continue the existing code.\n\n2. **Code Formatting:**\n   - **New Code Sections:** Use code blocks only when starting a completely new code section.\n   - **Avoid Mid-Continuation Blocks:** Do not start new code blocks in the middle of a continuation if the previous message did not close a code block.\n\n3. **Handling Incomplete Messages:**\n   - **Direct Continuation:** If a message is resubmitted for continuation, directly continue from the last point without altering the format.\n   - **No New Blocks:** Ensure no new code blocks are started unless the previous section was explicitly closed.\n\n4. **Adherence to Instructions:**\n   - **Follow Instructions Precisely:** Adhere strictly to the instructions provided in this prompt without expecting further clarification from the user during the task.\n   - **Consistency:** Maintain consistency in response format based on these guidelines.",
                    MaxTokens = chat.Settings.MaxTokens,
                    Temperature = chat.Settings.Temperature,
                    
                },
                // Copy all messages into a NEW list
                Messages = tempMessages
            };

            if (model.DeploymentType == DeploymentType.AzureOpenAI)
            {
                await HandleAzureOpenai(tempChat, model, responseMessage);
            }
            else
            {
                throw new ArgumentException("Model is not an AzureOpenAI model");

            }
        }




        chat.Updated = DateTimeTools.GetTimestamp();
        await _mediator.Publish(new UpdateWorkItemEvent { Chat = chat });
    }

    private async Task HandleAzureOpenai(WorkItemChat chat, AiModel model, Models.ChatMessage responseMessage)
    {
        try
        {
            var openAIService = new OpenAIService(model, _usernameService, _logger, _mediator);

            int inputTokens = openAIService.GetTokens(chat);
            await openAIService.GetStreamingResponse(chat, responseMessage, allowImages: model.Capabilities.Vision);

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
            var openAIService = new OpenAIService(model, _usernameService, _logger, _mediator);
            
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

    /// <summary>
    /// When you just want the response as a string
    /// No streaming handling needed
    /// </summary>
    /// <param name="chat"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<string> GetChatResponseAsString(WorkItemChat chat, AiModel? model = null)
    {
        string result = string.Empty;
        if(model == null)
        {
            model = _settingsService.DefaultModel;
        }

        if (model.DeploymentType == DeploymentType.AzureOpenAI)
        {
            var openAIService = new OpenAIService(model, _usernameService, _logger, _mediator);

            result = await openAIService.GetResponse(chat);
        }
        else
        {
            throw new Exception("Unsupported deployment type: " + model.DeploymentType);
        }

        return result;
    }

    public async Task<OpenAIEmbedding> GetEmbedding(string text, AiModel model)
    {
        if (model.DeploymentType == DeploymentType.AzureOpenAI)
        {
            var openAIService = new OpenAIService(model, _usernameService, _logger, _mediator);

            return await openAIService.GetEmbedding(text);
        }
        else
        {
            throw new Exception("Unsupported deployment type: " + model.DeploymentType);
        }
    }
}

public class OpenAIService : IOpenAIService
{
    private readonly IMediator _mediator;

    private AiModel _model { get; set; }
    private IUsernameService _usernameService { get; set; }
    private ILogger _logger { get; set; }
    private List<ChatTool> _tools { get; set; } = new List<ChatTool>();
    private ChatClient _client { get; set; }
    private AzureOpenAIClient _azureOpenAiClient { get; set; }
    private EmbeddingClient _embeddingClient { get; set; }

    public OpenAIService(AiModel model,
                         IUsernameService usernameService,
                         ILogger logger,                         
                         IMediator mediator)
    {
        if (model.DeploymentType != DeploymentType.AzureOpenAI)
        {
            throw new ArgumentException("Model is not an AzureOpenAI model");
        }
        _azureOpenAiClient = new AzureOpenAIClient(new Uri(model.Endpoint), new ApiKeyCredential(model.ApiKey));
        _client = _azureOpenAiClient.GetChatClient(model.DeploymentName);
        _embeddingClient = _azureOpenAiClient.GetEmbeddingClient(model.DeploymentName);
        _model = model;
        _usernameService = usernameService;
        _logger = logger;
        this._mediator = mediator;
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
            maxTokens = Math.Min(_model.MaxTokens, chat.Settings.MaxTokens);
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
                               await _usernameService.GetUsername(),
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

                    ToolChatMessage toolMessage = new(toolCall.Id, await ChatTools.HandleToolCall(toolCall));

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