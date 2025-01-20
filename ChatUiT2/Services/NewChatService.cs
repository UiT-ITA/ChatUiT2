using Azure.AI.OpenAI;
using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using ChatUiT2.Tools;
using OpenAI.Chat;
using System.ClientModel;
using Tiktoken;
using Tiktoken.Encodings;
using ZstdSharp.Unsafe;

namespace ChatUiT2.Services;

public class NewChatService
{
    private IUserService _userService { get; set; }
    private IConfigService _configService { get; set; }
    private ILogger _logger { get; set; }

    public NewChatService(IUserService userService, IConfigService configService, ILogger logger)
    {
        _userService = userService;
        _configService = configService;
        _logger = logger;
    }

    public async Task GetChatResponse(WorkItemChat chat)
    {
        Models.ChatMessage userMessage = chat.Messages.Last();

        Models.ChatMessage responseMessage = new Models.ChatMessage
        {
            Content = "",
            Role = Models.ChatMessageRole.Assistant, Status = ChatMessageStatus.Working
        };

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
            await HandleAzureOpenai(chat, model, endpoint);
        }
    }

    private async Task HandleAzureOpenai(WorkItemChat chat, Model model, ModelEndpoint endpoint)
    {
        try
        {
            var openAIService = new OpenAIService(model, endpoint);

            int inputTokens = openAIService.GetTokens(chat);
            var response = openAIService.GetStreamingResponse(chat, model, endpoint, allowFiles: true);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling AzureOpenAI");
        }
    }
}

public class OpenAIService
{
    private Model _model { get; set; }
    private ModelEndpoint _endpoint { get; set; }
    private ChatClient _client { get; set; }
    public OpenAIService(Model model, ModelEndpoint endpoint)
    {
        if (model.DeploymentType != "AzureOpenAI")
        {
            throw new ArgumentException("Model is not an AzureOpenAI model");
        }
        _client = new AzureOpenAIClient(new Uri(endpoint.Url), new ApiKeyCredential(endpoint.Key)).GetChatClient(model.DeploymentName);
        _model = model;
        _endpoint = endpoint;

    }


    public AsyncCollectionResult<StreamingChatCompletionUpdate> GetStreamingResponse(WorkItemChat chat, bool allowFiles = true)
    {

        var options = new ChatCompletionOptions()
        {
            MaxOutputTokenCount = Math.Min(_model.MaxTokens, chat.Settings.MaxTokens),
            Temperature = chat.Settings.Temperature,
            // Specify that usage should be included (how?)
        };


        int availableTokens = _model.MaxContext - (int)options.MaxOutputTokenCount;
        List<OpenAI.Chat.ChatMessage> messages = new();

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

        return _client.CompleteChatStreamingAsync(messages, options);
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
                tokens += GetFileTokens(model.DeploymentName, file);
            }
        }
        return tokens;
    }

    public int GetTokens(string content)
    {
        // Use tiktoken to calculate tokens
        Encoder encoder;
        if (_model.DeploymentName == "gpt-4o" || _model.DeploymentType == "gpt-4o-mini")
        {
            encoder = new Encoder(new O200KBase());
        }
        else
        {
            encoder = new Encoder(new Cl100KBase());
        }
        return encoder.CountTokens(content);
    }

    public int GetTokens(ChatFile file)
    {

        // Use tiktoken to calculate tokens
        Encoder encoder;
        if (_model.DeploymentName == "gpt-4o" || _model.DeploymentName == "gpt-4o-mini")
        {
            encoder = new Encoder(new O200KBase());
        }
        else
        {
            encoder = new Encoder(new Cl100KBase());
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
                if (_model.DeploymentName == "gpt-4o")
                {
                    imageTokens = 85 + 170 * (int)Math.Ceiling(imgPart.Width / 512.0) * (int)Math.Ceiling(imgPart.Height / 512.0);
                }
                else if (_model.DeploymentName == "gpt-4o-mini")
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
