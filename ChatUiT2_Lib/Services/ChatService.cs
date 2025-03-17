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
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ChatUiT2.Services;

public class ChatService : IChatService
{
    private readonly IUsernameService _usernameService;
    private readonly IMediator _mediator;
    private readonly IChatToolsService _chatToolsService;

    private ISettingsService _settingsService { get; set; }
    private ILogger _logger { get; set; }

    public ChatService(ISettingsService settingsService,
                       ILogger<ChatService> logger,
                       IUsernameService usernameService,
                       IMediator mediator,
                       IChatToolsService chatToolsService)
    {
        _settingsService = settingsService;
        _logger = logger;
        this._usernameService = usernameService;
        this._mediator = mediator;
        this._chatToolsService = chatToolsService;
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
            var openAIService = new OpenAIService(model, await _usernameService.GetUsername(), _logger, _mediator, _chatToolsService);

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
            var openAIService = new OpenAIService(model, await _usernameService.GetUsername(), _logger, _mediator, _chatToolsService);
            
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
            var openAIService = new OpenAIService(model, await _usernameService.GetUsername(), _logger, _mediator, _chatToolsService);

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
            var openAIService = new OpenAIService(model, await _usernameService.GetUsername(), _logger, _mediator, _chatToolsService);

            return await openAIService.GetEmbedding(text);
        }
        else
        {
            throw new Exception("Unsupported deployment type: " + model.DeploymentType);
        }
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