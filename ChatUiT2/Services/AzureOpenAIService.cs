using Azure.AI.OpenAI;
using Azure.AI.OpenAI.Chat;
using ChatUiT2.Models;
using MongoDB.Bson;
using OpenAI.Chat;
using System.ClientModel;
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
            foreach (var update in chatUpdate.ContentUpdate)
            content += update.Text;

        }

        return content;
    }

    public static AsyncResultCollection<StreamingChatCompletionUpdate> GetStreamingResponse(WorkItemChat chat, Model model, ModelEndpoint endpoint)
    {
        var client = new AzureOpenAIClient(new Uri(endpoint.Url), new Azure.AzureKeyCredential(endpoint.Key)).GetChatClient(model.DeploymentName);

        var options = new ChatCompletionOptions()
        {
            MaxTokens = Math.Min(model.MaxTokens, chat.Settings.MaxTokens),
            Temperature = chat.Settings.Temperature
        };

        int availableTokens = model.MaxContext - (int)options.MaxTokens;
        List<OpenAI.Chat.ChatMessage> messages = new ();

        messages.Add(new SystemChatMessage(chat.Settings.Prompt));

        for (int i = chat.Messages.Count - 1; i >= 0; i--)
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

            OpenAI.Chat.ChatMessage requestMessage = GetOpenAIMessage(message);


            messages.Insert(1, requestMessage);
            availableTokens -= messageTokens;
        }

        return client.CompleteChatStreamingAsync(messages, options);


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
