using Azure.AI.OpenAI;
using ChatUiT2.Models;

namespace ChatUiT2.Services;

public class AzureOpenAIService
{

    public StreamingResponse<StreamingChatCompletionsUpdate> GetStreamingResponse(ChatRequest request)
    {
        OpenAIClient client = new OpenAIClient(new Uri(_config.Endpoint), new Azure.AzureKeyCredential(_config.Key));

        var OAIRequest = new ChatCompletionsOptions()
        {
            DeploymentName = request.Model.DeploymentName,
            MaxTokens = Math.Min(request.Model.MaxTokens, request.Chat.Settings.MaxTokens),
            Temperature = request.Chat.Settings.Temperature,
            Messages =
            {
                new ChatRequestSystemMessage(request.Chat.Settings.Prompt)
            },
        };

        int availableTokens = request.Model.MaxContext - (int)OAIRequest.MaxTokens;
        for (int i  = request.Chat.Messages.Count - 1; i >= 0; i--)
        {
            var message = request.Chat.Messages[i];
            if (message.Status == ChatMessageStatus.Error) continue;
            int messageTokens = GetTokens(message.Content);

            if (messageTokens > availableTokens)
            {
                break;
            }

            if (message.Content == string.Empty)
            {
                continue;
            }

            ChatRequestMessage chatRequestMessage;
            if (message.Role == ChatMessageRole.User)
            {
                chatRequestMessage = new ChatRequestUserMessage(message.Content);
            }
            else if (message.Role == ChatMessageRole.Assistant)
            {
                chatRequestMessage = new ChatRequestAssistantMessage(message.Content);
            }
            else
            {
                throw new Exception("Unkown message role");
            }

            OAIRequest.Messages.Insert(1, chatRequestMessage);
            availableTokens -= messageTokens;
        }

        return client.GetChatCompletionsStreaming(OAIRequest);
    }

    private int GetTokens(string content)
    {
        return content.Length;
    }

    
}
