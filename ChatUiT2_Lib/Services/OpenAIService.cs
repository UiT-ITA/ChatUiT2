using Azure.AI.OpenAI;
using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using ChatUiT2.Models.Mediatr;
using ChatUiT2.Tools;
using ChatUiT2.Messaging;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using OpenAI.Embeddings;
using OpenAI.Responses;
using System.ClientModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tiktoken.Encodings;

namespace ChatUiT2.Services;

public class OpenAIService : IOpenAIService
{
    private readonly IPublisher _mediator;
    private readonly IChatToolsService _chatToolsService;

    private AiModel _model { get; set; }
    private string _username { get; set; }
    private ILogger _logger { get; set; }
    private List<ChatTool> _tools { get; set; } = new List<ChatTool>();
    private ChatClient _client { get; set; }
    private AzureOpenAIClient _azureOpenAiClient { get; set; }
    private EmbeddingClient _embeddingClient { get; set; }
#pragma warning disable OPENAI001
    private OpenAIResponseClient _responseClient { get; set; }
#pragma warning restore OPENAI001

    public OpenAIService(AiModel model,
                         string username,
                         ILogger logger,
                         IPublisher mediator,
                         IChatToolsService chatToolsService)
    {
        if (model.DeploymentType != DeploymentType.AzureOpenAI)
        {
            throw new ArgumentException("Model is not an AzureOpenAI model");
        }
        _azureOpenAiClient = new AzureOpenAIClient(new Uri(model.Endpoint), new ApiKeyCredential(model.ApiKey));
        _client = _azureOpenAiClient.GetChatClient(model.DeploymentName);
        _embeddingClient = _azureOpenAiClient.GetEmbeddingClient(model.DeploymentName);
#pragma warning disable OPENAI001
        _responseClient = _azureOpenAiClient.GetOpenAIResponseClient(model.DeploymentName);
#pragma warning restore OPENAI001
        _model = model;
        _username = username;
        _logger = logger;
        this._mediator = mediator;
        this._chatToolsService = chatToolsService;
    }

    public async Task<string> GetResponse(WorkItemChat chat, bool allowFiles = false)
    {
        int availableTokens = _model.MaxContext - chat.Settings.MaxTokens;

        // GPT-5.4/5.5/5.6 reject the Chat Completions API for reasoning and must use the
        // Responses API (same routing as GetStreamingResponse). This non-streaming path is
        // used for short utility completions (e.g. chat naming, RAG helpers); no tools are
        // added and no stream events are published, so it is safe when the mediator is null.
        if (_model.Capabilities.UseResponsesApi)
        {
#pragma warning disable OPENAI001
            var responseOptions = new ResponseCreationOptions
            {
                StoredOutputEnabled = false,
            };

            if (_model.Capabilities.ReasoningEffortLevel is not null)
            {
                responseOptions.ReasoningOptions = new ResponseReasoningOptions
                {
                    ReasoningEffortLevel = new ResponseReasoningEffortLevel(_model.Capabilities.ReasoningEffortLevel.Value.ToString())
                };
            }

            List<ResponseItem> inputItems = GetResponseInputItems(chat, availableTokens, allowFiles);
            ClientResult<OpenAIResponse> responseResult = await _responseClient.CreateResponseAsync(inputItems, responseOptions);
#pragma warning restore OPENAI001
            return responseResult.Value.GetOutputText() ?? string.Empty;
        }

        var messages = GetOpenAiMessages(chat, availableTokens, allowFiles);

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
        // GPT-5.4/5.5 reject function tools + reasoning_effort on the Chat Completions API,
        // so they are served through the Responses API instead.
        if (_model.Capabilities.UseResponsesApi)
        {
            await GetResponsesApiStreamingResponse(chat, responseMessage, allowImages);
            return;
        }

        var options = new ChatCompletionOptions();

        int maxTokens = _model.MaxTokens;

        if (_model.Capabilities.ReasoningEffortLevel is not null)
        {
#pragma warning disable OPENAI001
            options.ReasoningEffortLevel = _model.Capabilities.ReasoningEffortLevel;
#pragma warning restore OPENAI001
        }
        else if (_model.ModelName == ModelName.gpt_5_mini || _model.ModelName == ModelName.gpt_5)
        {
            //options.MaxOutputTokenCount = _model.MaxTokens;
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
        try
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

                        string toolResult = await _chatToolsService.HandleToolCall(toolCall);

                        // Generated images come back as a (potentially multi-MB) base64 data
                        // URI. Render it straight to the user instead of feeding it back to the
                        // model, which would explode the context and can't reproduce it anyway.
                        if (toolCall.FunctionName == "GetImageGeneration" && toolResult.StartsWith("data:image"))
                        {
                            responseMessage.Content += $"\n\n![Generated image]({toolResult})\n\n";
                            await _mediator.Publish(new StreamUpdatedEvent());
                            toolResult = "The image was generated successfully and has been shown to the user.";
                        }

                        ToolChatMessage toolMessage = new(toolCall.Id, toolResult);

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
        catch (ClientResultException ex) when (ex.Message.Contains("context_length_exceeded"))
        {
            responseMessage.Content = "❌ **Conversation Too Long**\n\nThe conversation has become too long for the AI model to process. Please start a new conversation or remove some earlier messages to continue.";
            responseMessage.Status = ChatMessageStatus.Error;
            _logger?.LogWarning("Context length exceeded for user {User} with model {Model}", _username, _model.DeploymentName);
        }
        catch (ClientResultException ex) when (ex.Status == 400)
        {
            responseMessage.Content = "❌ **Request Error**\n\nThere was an issue processing your request. Please try again or start a new conversation.";
            responseMessage.Status = ChatMessageStatus.Error;
            _logger?.LogError(ex, "Azure OpenAI request failed with status 400 for user {User}", _username);
        }
        catch (Exception ex)
        {
            responseMessage.Content = "❌ **Unexpected Error**\n\nAn unexpected error occurred. Please try again later.";
            responseMessage.Status = ChatMessageStatus.Error;
            _logger?.LogError(ex, "Unexpected error in CompleteChatStreamingAsync for user {User}", _username);
        }
    }


#pragma warning disable OPENAI001 // Responses API types are still flagged as evaluation-only in the SDK
    // Responses-API equivalent of GetStreamingResponse, used for models where the Chat
    // Completions API rejects function tools combined with reasoning_effort (GPT-5.4/5.5).
    private async Task GetResponsesApiStreamingResponse(WorkItemChat chat, Models.ChatMessage responseMessage, bool allowImages)
    {
        var options = new ResponseCreationOptions
        {
            // Keep requests stateless: the app stores and encrypts history itself and resends
            // the full input each turn, so there is no need for server-side response storage.
            StoredOutputEnabled = false,
        };

        if (_model.Capabilities.ReasoningEffortLevel is not null)
        {
            options.ReasoningOptions = new ResponseReasoningOptions
            {
                // Map the Chat-Completions effort value (incl. "none") onto the Responses enum.
                ReasoningEffortLevel = new ResponseReasoningEffortLevel(_model.Capabilities.ReasoningEffortLevel.Value.ToString())
            };
        }

        foreach (var tool in _model.RequiredTools)
        {
            options.Tools.Add(ToResponseTool(tool.Tool));
        }
        foreach (var tool in _model.OptionalTools)
        {
            if (tool.Selected)
            {
                options.Tools.Add(ToResponseTool(tool.Tool));
            }
        }

        int maxTokens = _model.MaxTokens;
        int availableTokens = _model.MaxContext - maxTokens;
        List<ResponseItem> inputItems = GetResponseInputItems(chat, availableTokens, allowImages);

        int inputTokens = GetTokens(chat);

        await CompleteResponseStreamingAsync(inputItems, options, responseMessage);

        int outputTokens = GetTokens(responseMessage.Content);

        _logger.LogInformation("Type: {LogType}, User: {User}, Model: {Model} Input: {Input}, Output: {Output}",
                               "ChatRequest",
                               _username,
                               _model.DeploymentName,
                               inputTokens,
                               outputTokens);
    }

    // The tools are defined once as Chat Completions ChatTool; convert each to the Responses
    // equivalent using the same name/description/parameters schema.
    private static ResponseTool ToResponseTool(ChatTool tool)
    {
        return ResponseTool.CreateFunctionTool(
            functionName: tool.FunctionName,
            functionDescription: tool.FunctionDescription,
            functionParameters: tool.FunctionParameters);
    }

    private async Task CompleteResponseStreamingAsync(List<ResponseItem> inputItems, ResponseCreationOptions options, Models.ChatMessage responseMessage)
    {
        try
        {
            var response = _responseClient.CreateResponseStreamingAsync(inputItems, options);

            List<FunctionCallResponseItem> functionCalls = new();
            bool incomplete = false;

            await foreach (StreamingResponseUpdate update in response)
            {
                if (update is StreamingResponseOutputTextDeltaUpdate textDelta)
                {
                    responseMessage.Content += textDelta.Delta;
                    await _mediator.Publish(new StreamUpdatedEvent());
                }
                else if (update is StreamingResponseOutputItemDoneUpdate itemDone
                         && itemDone.Item is FunctionCallResponseItem functionCall)
                {
                    functionCalls.Add(functionCall);
                }
                else if (update is StreamingResponseIncompleteUpdate)
                {
                    incomplete = true;
                }
            }

            if (functionCalls.Count > 0)
            {
                await _mediator.Publish(new StreamUpdatedEvent());

                foreach (var functionCall in functionCalls)
                {
                    // In stateless mode the model's own function call must be echoed back in the
                    // next request alongside its output, or the API rejects the output item.
                    inputItems.Add(ResponseItem.CreateFunctionCallItem(
                        functionCall.CallId, functionCall.FunctionName, functionCall.FunctionArguments));

                    if (responseMessage.Content == "" || responseMessage.Content[^1] == '\n')
                    {
                        responseMessage.Content += GetToolNotice(functionCall.FunctionName, functionCall.FunctionArguments);
                    }
                    else
                    {
                        responseMessage.Content += "\n" + GetToolNotice(functionCall.FunctionName, functionCall.FunctionArguments);
                    }
                    await _mediator.Publish(new StreamUpdatedEvent());

                    string toolResult = await _chatToolsService.HandleToolCall(functionCall.FunctionName, functionCall.FunctionArguments);

                    // Generated images come back as a base64 data URI; render it to the user and
                    // feed only a short confirmation back to the model (same as the Chat path).
                    if (functionCall.FunctionName == "GetImageGeneration" && toolResult.StartsWith("data:image"))
                    {
                        responseMessage.Content += $"\n\n![Generated image]({toolResult})\n\n";
                        await _mediator.Publish(new StreamUpdatedEvent());
                        toolResult = "The image was generated successfully and has been shown to the user.";
                    }

                    inputItems.Add(ResponseItem.CreateFunctionCallOutputItem(functionCall.CallId, toolResult));
                }

                await CompleteResponseStreamingAsync(inputItems, options, responseMessage);
                return;
            }

            responseMessage.Status = incomplete ? ChatMessageStatus.TokenLimit : ChatMessageStatus.Done;
        }
        catch (ClientResultException ex) when (ex.Message.Contains("context_length_exceeded"))
        {
            responseMessage.Content = "❌ **Conversation Too Long**\n\nThe conversation has become too long for the AI model to process. Please start a new conversation or remove some earlier messages to continue.";
            responseMessage.Status = ChatMessageStatus.Error;
            _logger?.LogWarning("Context length exceeded for user {User} with model {Model}", _username, _model.DeploymentName);
        }
        catch (ClientResultException ex) when (ex.Status == 400)
        {
            responseMessage.Content = "❌ **Request Error**\n\nThere was an issue processing your request. Please try again or start a new conversation.";
            responseMessage.Status = ChatMessageStatus.Error;
            _logger?.LogError(ex, "Azure OpenAI Responses request failed with status 400 for user {User}", _username);
        }
        catch (Exception ex)
        {
            responseMessage.Content = "❌ **Unexpected Error**\n\nAn unexpected error occurred. Please try again later.";
            responseMessage.Status = ChatMessageStatus.Error;
            _logger?.LogError(ex, "Unexpected error in CompleteResponseStreamingAsync for user {User}", _username);
        }
    }

    // Builds the Responses-API input items, mirroring GetOpenAiMessages: system prompt first,
    // then the most recent messages that fit the token budget, in chronological order.
    private List<ResponseItem> GetResponseInputItems(WorkItemChat chat, int maxTokens, bool allowImages = true)
    {
        List<ResponseItem> items = new();
        int availableTokens = maxTokens;

        items.Add(ResponseItem.CreateSystemMessageItem(chat.Settings.Prompt));

        for (int i = chat.Messages.Count - 1; i >= 0; i--)
        {
            var message = chat.Messages[i];
            if (message.Status == ChatMessageStatus.Error) continue;

            int messageTokens = GetTokens(StripInlineImages(message.Content));
            int fileTokens = 0;
            foreach (var file in message.Files)
            {
                fileTokens += GetTokens(file);
            }

            if (messageTokens + fileTokens > availableTokens)
            {
                break;
            }

            if (message.Content == string.Empty && (!allowImages || message.Files.Count == 0))
            {
                continue;
            }

            ResponseItem item;
            if (message.Role == Models.ChatMessageRole.User)
            {
                if (message.Files.Count > 0)
                {
                    var parts = new List<ResponseContentPart>
                    {
                        ResponseContentPart.CreateInputTextPart(
                            string.IsNullOrEmpty(message.Content) ? "Here is a list of files:\n" : StripInlineImages(message.Content))
                    };
                    foreach (var file in message.Files)
                    {
                        parts.Add(ResponseContentPart.CreateInputTextPart("File: " + file.FileName + "\n"));
                        foreach (var part in file.Parts)
                        {
                            if (part is TextFilePart textPart)
                            {
                                parts.Add(ResponseContentPart.CreateInputTextPart(textPart.Data));
                            }
                            else if (part is ImageFilePart imagePart && allowImages)
                            {
                                parts.Add(ResponseContentPart.CreateInputImagePart(new BinaryData(imagePart.Data), "image/png"));
                            }
                        }
                    }
                    item = ResponseItem.CreateUserMessageItem(parts);
                }
                else
                {
                    item = ResponseItem.CreateUserMessageItem(StripInlineImages(message.Content));
                }
            }
            else
            {
                item = ResponseItem.CreateAssistantMessageItem(StripInlineImages(message.Content));
            }

            items.Insert(1, item);
            availableTokens -= messageTokens;
        }

        return items;
    }
#pragma warning restore OPENAI001



    private string GetToolNotice(ChatToolCall toolCall)
    {
        return GetToolNotice(toolCall.FunctionName, toolCall.FunctionArguments);
    }

    private string GetToolNotice(string functionName, BinaryData functionArguments)
    {
        // Return string in the form "> 👀**functionName(argumentName1 = argument1, argumentname2 = argument2)**\n"

        StringBuilder notice = new();
        notice.Append("> 🔎**");
        notice.Append(functionName);
        notice.Append("**(");
        var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(functionArguments.ToString());
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
            int messageTokens = GetTokens(StripInlineImages(message.Content));

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
            return new UserChatMessage(StripInlineImages(message.Content));
        }
        else if (message.Role == Models.ChatMessageRole.Assistant)
        {
            return new AssistantChatMessage(StripInlineImages(message.Content));
        }
        else
        {
            throw new Exception("Unkown message role");
        }
    }

    private static readonly Regex _inlineDataImageRegex =
        new(@"!\[[^\]]*\]\(data:[^)]*\)", RegexOptions.Compiled);

    // Generated images are stored inline in the message content as a base64 data URI
    // so the UI can render them. They must not be sent back to the model: the payload
    // is huge and the model can't reproduce it. Replace them with a short placeholder.
    private static string StripInlineImages(string content)
    {
        return _inlineDataImageRegex.Replace(content, "[generated image]");
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

    public async Task<string> GetResponseRaw(List<OpenAI.Chat.ChatMessage> messages, ChatCompletionOptions options)
    {
        ChatCompletion response = await _client.CompleteChatAsync(messages, options);
        return response.Content[0].Text;
    }

    public IAsyncEnumerable<StreamingChatCompletionUpdate> GetStreamingResponseRaw(List<OpenAI.Chat.ChatMessage> messages, ChatCompletionOptions options)
    {
        return _client.CompleteChatStreamingAsync(messages, options);
    }
}
