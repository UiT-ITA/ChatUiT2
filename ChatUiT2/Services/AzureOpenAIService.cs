using Azure.AI.OpenAI;
using Azure.AI.OpenAI.Chat;
using ChatUiT2.Models;
using ChatUiT2.Tools;
using MongoDB.Bson;
using OpenAI.Chat;
using System.ClientModel;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Text.Json;
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

    public static AsyncCollectionResult<StreamingChatCompletionUpdate> GetStreamingResponse(WorkItemChat chat, Model model, ModelEndpoint endpoint, bool allowFiles = false)
    {
        var client = new AzureOpenAIClient(new Uri(endpoint.Url), new ApiKeyCredential(endpoint.Key)).GetChatClient(model.DeploymentName);

        var options = new ChatCompletionOptions()
        {
            MaxOutputTokenCount = Math.Min(model.MaxTokens, chat.Settings.MaxTokens),
            Temperature = chat.Settings.Temperature,
            // Specify that usage should be included (how?)
        };


        int availableTokens = model.MaxContext - (int)options.MaxOutputTokenCount;
        List<OpenAI.Chat.ChatMessage> messages = new();

        messages.Add(new SystemChatMessage(chat.Settings.Prompt));

        for (int i = chat.Messages.Count - 1; i >= 0; i--)
        {
            var message = chat.Messages[i];
            if (message.Status == ChatMessageStatus.Error) continue;
            int messageTokens = GetTokens(model.DeploymentName, message.Content);
            int fileTokens = 0;
            if (allowFiles)
            {
                foreach (var file in message.Files)
                {
                    fileTokens += GetFileTokens(model.DeploymentName, file);
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

        /*for (int i = chat.Messages.Count - 1; i >= 0; i--)
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

            OpenAI.Chat.ChatMessage requestMessage;
            if (allowFiles)
            {
                if (message.Files.Count > 0)
                {
                    messages.Insert(1, GetOpenAIMessage(message));
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
        }*/


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

    //public static OpenAI.Chat.ChatMessage ChatMessageWithFiles(Models.ChatMessage message)
    //{
    //    if (message.Role != Models.ChatMessageRole.User)
    //    {
    //        return new AssistantChatMessage(message.Content);
    //    }

    //    List<ChatMessageContentPart> messageContentParts = new ();
    //    foreach (var file in message.Files)
    //    {
    //        if (file.Bytes == null)
    //        {
    //            throw new Exception("File is empty");
    //        }

    //        if (file.FileType == FileTypeOld.Image)
    //        {   
    //            var messagePart = ChatMessageContentPart.CreateImagePart(
    //                imageBytes: new BinaryData(file.Bytes!),
    //                imageBytesMediaType: FileTools.GetMimeTypeFromFile(file)
    //                );
    //            messageContentParts.Add(messagePart);
    //        }
    //        else
    //        {
    //            // filename has the form 3749873294_file_name.txt I want to get the file_name.txt
    //            int underscoreIndex = file.FileName.IndexOf('_');
    //            string fileName = file.FileName.Substring(underscoreIndex + 1);


    //            string fileText = FileTools.GetTextFromFile(file);
    //            string messageText = "This is a file named " + fileName + ":\n" + fileText + "\n\n";

    //            var messagePart = ChatMessageContentPart.CreateTextPart(messageText);
    //            messageContentParts.Add(messagePart);
    //        }
    //    }
    //    messageContentParts.Add(ChatMessageContentPart.CreateTextPart(message.Content));
    //    return new UserChatMessage(messageContentParts);
    //}

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

    public static int GetFileTokens(string model, ChatFile file)
    {

        // Use tiktoken to calculate tokens
        Encoder encoder;
        if (model == "gpt-4o" || model == "gpt-4o-mini")
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
                if (model == "gpt-4o")
                {
                    imageTokens = 85 + 170 * (int)Math.Ceiling(imgPart.Width / 512.0) * (int)Math.Ceiling(imgPart.Height / 512.0);
                }
                else if (model == "gpt-4o-mini")
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

    public static int GetChatTokens(WorkItemChat chat, Model model)
    {
        int tokens = 0;
        foreach (var message in chat.Messages)
        {
            tokens += GetTokens(model.DeploymentName, message.Content);
            foreach (var file in message.Files)
            {
                tokens += GetFileTokens(model.DeploymentName, file);
            }
        }
        return tokens;
    }

}



