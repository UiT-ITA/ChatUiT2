using ChatUiT2.Interfaces;
using ChatUiT2.Models;
using ChatUiT2.Models.RagProject;
using ChatUiT2.Tools;
using OpenAI.Chat;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tiktoken.Encodings;

namespace ChatUiT2.Services;

public class ChatToolsService : IChatToolsService
{
    private readonly IRagDatabaseService _ragDatabaseService;

    public ChatToolsService(IRagDatabaseService ragDatabaseService)
    {
        this._ragDatabaseService = ragDatabaseService;
    }
    public async Task<string> GetTopdesk(string query)
    {
        RagProject? ragProject = await _ragDatabaseService.GetRagProjectByName("TopdeskKnowledgeItems");
        var ragSearchResult = await _ragDatabaseService.DoGenericRagSearch(ragProject, query);
        StringBuilder stringResult = new();
        stringResult.Append("Here are some knowledge item articles that i want you to base your answer on.\n\n");
        foreach (var result in ragSearchResult)
        {
            stringResult.Append($"-- Article start id {result.SourceId} url to article {result.ContentUrl}\n");
            stringResult.Append(result.SourceContent);
            stringResult.Append($"-- Article end id {result.SourceId}\n\n");
        }
        return stringResult.ToString();
    }

    public async Task<string> HandleToolCall(ChatToolCall toolCall)
    {
        try
        {
            using JsonDocument argumentsDocument = JsonDocument.Parse(toolCall.FunctionArguments);
            switch (toolCall.FunctionName)
            {
                case "getTopdesk":
                    if (!argumentsDocument.RootElement.TryGetProperty("query", out JsonElement locationElement))
                    {
                        return "This tool needs a valid query";
                    }
                    else
                    {
                        string query = locationElement.GetString()!;

                        return await GetTopdesk(query);
                    }
                case "GetWikipediaEntry":
                    if (!argumentsDocument.RootElement.TryGetProperty("topic", out JsonElement topicElement))
                    {
                        return "This tool needs a valid topic";
                    }
                    else
                    {
                        return await GetWikipedia(topicElement.GetString()!);
                    }
                case "GetWebpage":
                    if (!argumentsDocument.RootElement.TryGetProperty("url", out JsonElement urlElement))
                    {
                        return "This tool needs a valid URL";
                    }
                    else
                    {
                        return await GetWebpage(urlElement.GetString()!);
                    }
                default:
                    return "Sorry, I don't know how to handle this tool.";
            }

        }
        catch (Exception)
        {
            return "Sorry, I don't know how to handle this tool.";
        }
    }

    public async Task<string> GetWikipedia(string topic)
    {
        Console.WriteLine($"GetWikipedia: {topic}");
        return await WikipeidaHelper.GetWikipediaFirstSectionAsync(topic);
    }

    public async Task<string> GetWebpage(string url)
    {
        Console.WriteLine($"GetWebpage: {url}");

        HttpClient client = new HttpClient();
        try
        {
            string response = await client.GetStringAsync(url);
            return TrimContent(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return "An error occurred while fetching the webpage";
        }
    }

    private string TrimContent(string content, int tokens = 20_000, ModelName model = ModelName.gpt_4o_mini)
    {

        var content1 = StripHTML(content);

        Console.WriteLine($"Content length: {content.Length}");
        Console.WriteLine($"Content1 length: {content1.Length}");

        Console.WriteLine(content1);

        content = content1;

        Tiktoken.Encoder encoder;

        if (model == ModelName.gpt_4o_mini)
        {
            encoder = new Tiktoken.Encoder(new O200KBase());
        }
        else
        {
            throw new NotImplementedException();
        }

        var encoded = encoder.Encode(content);

        if (encoded.Count <= tokens)
        {
            return content;
        }

        var trimmed = encoder.Decode(encoded.Take(tokens).ToList());

        return trimmed;
    }

    private string StripHTML(string htmlContent)
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(htmlContent);

        // Remove unwanted elements but preserve links
        var nodesToRemove = doc.DocumentNode.SelectNodes(
            "//script|//style|//iframe|//comment()|//head|//meta");
        if (nodesToRemove != null)
        {
            foreach (var node in nodesToRemove)
            {
                node.Remove();
            }
        }

        // Replace links with a compact format [text](url)
        var links = doc.DocumentNode.SelectNodes("//a[@href]");
        if (links != null)
        {
            foreach (var link in links)
            {
                var href = link.GetAttributeValue("href", "");
                var text = link.InnerText.Trim();
                if (!string.IsNullOrEmpty(href) && !string.IsNullOrEmpty(text))
                {
                    var newNode = doc.CreateTextNode($"[{text}]({href}) ");
                    link.ParentNode.ReplaceChild(newNode, link);
                }
            }
        }

        // Get text and clean it up
        string innerText = doc.DocumentNode.InnerText;
        innerText = Regex.Replace(innerText, @"\s+", " ");
        innerText = Regex.Replace(innerText, @"[\r\n]+", "\n");

        return innerText.Trim();
    }
}
