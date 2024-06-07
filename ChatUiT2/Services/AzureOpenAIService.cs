using Azure.AI.OpenAI;
using ChatUiT2.Models;
using Tiktoken;
using Tiktoken.Encodings;

namespace ChatUiT2.Services;

public static class AzureOpenAIService
{
    public static async Task<string> GetResponse(WorkItemChat chat, Model model, ModelEndpoint endpoint)
    {
        // Get the response from the OpenAI API
        var response = GetStreamingResponse(chat, model, endpoint);
        string content = "";
        await foreach (var chatUpdate in response)
        {
            content += chatUpdate.ContentUpdate;
        }

        return content;
    }

    public static StreamingResponse<StreamingChatCompletionsUpdate> GetStreamingResponse(WorkItemChat chat, Model model, ModelEndpoint endpoint)
    {
        OpenAIClient client = new OpenAIClient(new Uri(endpoint.Url), new Azure.AzureKeyCredential(endpoint.Key));

        var OAIRequest = new ChatCompletionsOptions()
        {
            DeploymentName = model.DeploymentName,
            MaxTokens = Math.Min(model.MaxTokens, chat.Settings.MaxTokens),
            Temperature = chat.Settings.Temperature,
            Messages =
            {
                new ChatRequestSystemMessage(chat.Settings.Prompt)
            },
        };

        int availableTokens = model.MaxContext - (int)OAIRequest.MaxTokens;
        for (int i  = chat.Messages.Count - 1; i >= 0; i--)
        {
            var message = chat.Messages[i];
            if (message.Status == ChatMessageStatus.Error) continue;
            int messageTokens = GetTokens(model.DeploymentName, message.Content);

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

    public static int GetTokens(string model, string content)
    {
        // Use tiktoken to calculate tokens
        Encoder encoder;
        if (model == "gpt-4o")
        {
            encoder = new Encoder(new O200KBase());
        }
        else
        {
            encoder = new Encoder(new Cl100KBase());
        }

        return encoder.CountTokens(content);
    }

    
}
