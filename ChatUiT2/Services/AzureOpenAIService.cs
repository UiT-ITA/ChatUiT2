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

    public static AsyncResultCollection<StreamingChatCompletionUpdate> GetStreamingResponse(WorkItemChat chat, Model model, ModelEndpoint endpoint, bool allowFiles = false)
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
            Console.WriteLine("Processing message: " + chat.Messages[i].Role + ": " + chat.Messages[i].Content );
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

            OpenAI.Chat.ChatMessage requestMessage;
            if (allowFiles)
            {
                requestMessage = ChatMessageWithFiles(message);
            }
            else
            {
                requestMessage = GetOpenAIMessage(message);
            }


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

    public static OpenAI.Chat.ChatMessage ChatMessageWithFiles(Models.ChatMessage message)
    {
        if (message.Role != Models.ChatMessageRole.User) {
            return new AssistantChatMessage(message.Content);
        }

        List<ChatMessageContentPart> messageContentParts = new List<ChatMessageContentPart>();
        foreach (var file in message.Files)
        {
            if (file.Bytes == null)
            {
                throw new Exception("File is empty");
            }

            if (file.FileType == FileType.Image)
            {
                var messagePart = ChatMessageContentPart.CreateImageMessageContentPart(
                    imageBytes: new BinaryData(file.Bytes!),
                    imageBytesMediaType: ChatFile.GetMimeType(file)
                    );
                messageContentParts.Add(messagePart);
            }
            else if (file.FileType == FileType.Document)
            {
                if (file.FileName.Split(".").Last() == "txt")
                {
                    // filename has the form 3749873294_file_name.txt I want to get the file_name.txt
                    int underscoreIndex = file.FileName.IndexOf('_');
                    string fileName = file.FileName.Substring(underscoreIndex + 1);


                    string fileText = ChatFile.GetText(file);
                    string messageText = "This is a file named " + fileName + ":\n" + fileText + "\n\n";

                    var messagePart = ChatMessageContentPart.CreateTextMessageContentPart(messageText);
                    messageContentParts.Add(messagePart);
                }
                else
                {
                    throw new Exception("Unsupported document type: " + file.FileName.Split(".").Last());
                }
            }
            else if (file.FileType == FileType.Data)
            {
            }
        }

        messageContentParts.Add(ChatMessageContentPart.CreateTextMessageContentPart(message.Content));

        return new UserChatMessage(messageContentParts);
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
