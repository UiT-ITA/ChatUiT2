using ChatUiT2.Interfaces;
using ChatUiT2.Models.RagProject;
using ChatUiT2.Models;
using HtmlAgilityPack;
using MudBlazor;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using Tiktoken.Encodings;
using MediatR;
using ChatUiT2.Models.Mediatr;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ChatUiT2.Services;

public class ChatToolsService : IChatToolsService
{
    private readonly IMediator _mediator;
    private readonly IRagSearchService _ragSearchService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ChatToolsService> _logger;
    private readonly IRagDatabaseService _ragDatabaseService;

    public ChatToolsService(IMediator mediator,
                            IRagSearchService ragSearchService,
                            ISettingsService settingsService,
                            ILogger<ChatToolsService> logger,
                            IRagDatabaseService ragDatabaseService)
    {
        this._mediator = mediator;
        this._ragSearchService = ragSearchService;
        this._settingsService = settingsService;
        this._logger = logger;
        this._ragDatabaseService = ragDatabaseService;
    }

    public async Task<string> GetTopdesk(string query)
    {
        RagProject? ragProject = await _ragDatabaseService.GetRagProjectByName("TopdeskKnowledgeItems");
        if (ragProject == null)
        {
            return "Could not find the TopdeskKnowledgeItems project";
        }

        var model = _settingsService.EmbeddingModel;
        var openAIService = new OpenAIService(model, "System", _logger, _mediator, null!);
        var embedding = await openAIService.GetEmbedding(query);

        var ragSearchResult = await _ragSearchService.DoGenericRagSearch(ragProject, embedding, 3, 0.6d);
        StringBuilder stringResult = new();
        stringResult.Append("Here are some knowledge item articles that i want you to base your answer on.\n");
        stringResult.Append("Include links to the articles that were most relevant for finding the answer using url and title of article.");
        stringResult.Append("Append the article number in paranthesis after title and use the concatinated string as link text.\n\n");
        foreach (var result in ragSearchResult)
        {
            stringResult.Append($"-- Article start id {result.SourceId} url to article is {result.ContentUrl} title of article is \"{result.ContentTitle}\" article number is {result.SourceAltId}\n");
            stringResult.Append(result.SourceContent);
            stringResult.Append($"-- Article end id {result.SourceId}\n\n");
        }
        return stringResult.ToString();
    }

    public string GenerateImage(string description)
    {
        Console.WriteLine($"GenerateImage: {description}");
        return "Not implemented";
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
}
public class GenerateImage
{
    public async Task<string> GenerateImageAsync(string description)
    {
        // Call to an image generation API or service
        // This is a placeholder implementation
        await Task.Delay(1000); // Simulate async call
        Console.WriteLine($"Generating image based on description: {description}");
        //return $"https://image.service/api/generate?description={Uri.EscapeDataString(description)}";
        return "https://commons.wikimedia.org/wiki/Example_images#/media/File:Example.png";
    }
}


public static class WikipeidaHelper
{
    public static async Task<string> GetWikipediaFirstSectionAsync(string topic)
    {
        HttpClient client = new HttpClient();
        try
        {
            string url = $"https://en.wikipedia.org/w/api.php?action=parse&page={Uri.EscapeDataString(topic)}&prop=text&format=json";
            string response = await client.GetStringAsync(url);
            JObject json = JObject.Parse(response);

            if (json["error"] != null)
            {
                return json["error"]!["info"]!.ToString();
            }

            string html = json["parse"]!["text"]!["*"]!.ToString();

            string? redirectTopic = GetRedirectTopic(html);

            if (redirectTopic != null)
            {
                return await GetWikipediaFirstSectionAsync(redirectTopic);
            }

            string? firstSection = ExtractFirstSection(html);
            string? infobox = ExtractInfobox(html);

            if (firstSection == null)
            {
                return "Topic not found";
            }

            return firstSection + "Facts:\n" + infobox;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return "Topic not found";
        }
    }

    public static async Task<string> GetWikipediaSectionAsync(string topic, string section)
    {
        await Task.Delay(1000); // Simulate async call
        return "";
    }

    private static string? GetRedirectTopic(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var redirectNode = document.DocumentNode.SelectSingleNode("//div[@class='redirectMsg']");
        if (redirectNode != null)
        {
            var anchorNode = redirectNode.SelectSingleNode(".//a");
            if (anchorNode != null)
            {
                return anchorNode.InnerText;
            }
        }
        return null;
    }

    private static string? ExtractFirstSection(string html)
    {
        // Find the end of the infobox
        var infoboxEnd = html.IndexOf("</table>");
        if (infoboxEnd == -1)
        {
            Console.WriteLine("Infobox end not found.");
            return null;
        }
        // Find the first <p> tag after the infobox
        var firstParagraphStart = html.IndexOf("<p>", infoboxEnd);
        if (firstParagraphStart == -1)
        {
            Console.WriteLine("First paragraph start not found.");
            return null;
        }
        // Find the end of the first section using the <h2> tag
        var firstSectionEnd = html.IndexOf("<h2", firstParagraphStart);
        if (firstSectionEnd == -1)
        {
            Console.WriteLine("First section end not found.");
            return null;
        }
        // Extract the HTML for the first section
        string firstSectionHtml = html.Substring(firstParagraphStart, firstSectionEnd - firstParagraphStart);
        // Strip HTML tags to get plain text
        return StripHtmlTags(firstSectionHtml);
    }

    private static string? ExtractInfobox(string html)
    {
        var infoboxStart = html.IndexOf("infobox");
        if (infoboxStart == -1) return null;
        var infoboxEnd = html.IndexOf("</table>", infoboxStart);
        if (infoboxEnd == -1) return null;
        string infoboxHtml = html.Substring(infoboxStart - 10, infoboxEnd - infoboxStart + 10);
        //return infoboxHtml;
        return ExtractAndFormatInfobox(infoboxHtml);
    }

    private static string? ExtractAndFormatInfobox(string infoboxHtml)
    {
        var document = new HtmlAgilityPack.HtmlDocument();
        document.LoadHtml(infoboxHtml);
        var rows = document.DocumentNode.SelectNodes("//tr");
        if (rows == null) return null;
        var result = new System.Text.StringBuilder();
        foreach (var row in rows)
        {
            var headerNode = row.SelectSingleNode(".//th");
            var dataNode = row.SelectSingleNode(".//td");
            if (headerNode != null && dataNode != null)
            {
                var headerText = HtmlEntity.DeEntitize(headerNode.InnerText.Trim());
                // Remove <sup> elements (citations) from the data node
                foreach (var sup in dataNode.SelectNodes(".//sup") ?? Enumerable.Empty<HtmlNode>())
                {
                    sup.Remove();
                }
                var dataText = HtmlEntity.DeEntitize(dataNode.InnerText.Trim());
                // Check for nested tables
                var nestedTable = dataNode.SelectSingleNode(".//table");
                if (nestedTable != null)
                {
                    var nestedRows = nestedTable.SelectNodes(".//tr");
                    if (nestedRows != null && nestedRows.Count > 1)
                    {
                        var subHeaders = nestedRows[0].SelectNodes(".//th");
                        var subData = nestedRows[1].SelectNodes(".//td");
                        if (subHeaders != null && subData != null)
                        {
                            var subHeaderText = string.Join(", ", subHeaders.Skip(1).Select(sh => HtmlEntity.DeEntitize(sh.InnerText.Trim())));
                            var subDataText = string.Join(", ", subData.Select(sd => HtmlEntity.DeEntitize(sd.InnerText.Trim())));
                            dataText = subDataText;
                            headerText = $"{HtmlEntity.DeEntitize(subHeaders[0].InnerText.Trim())} ({subHeaderText})";
                        }
                    }
                }
                else
                {
                    // Handle list items within a single data cell
                    var listItems = dataNode.SelectNodes(".//li");
                    if (listItems != null)
                    {
                        var formattedItems = listItems.Select(li => HtmlEntity.DeEntitize(li.InnerText.Trim()));
                        dataText = string.Join("; ", formattedItems);
                    }
                }
                // Only append if the header is meaningful and not a citation or similar
                if (!string.IsNullOrWhiteSpace(headerText) && !headerText.StartsWith(" ["))
                {
                    result.AppendLine($"{headerText}: {dataText}");
                }
            }
        }
        return result.ToString();
    }

    private static string StripHtmlTags(string html)
    {
        // Use a regular expression to remove HTML tags
        var noHtml = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
        // Use a regular expression to remove citation markers like [7]
        var noCitations = System.Text.RegularExpressions.Regex.Replace(noHtml, @"\[\d+\]", string.Empty);
        // Decode HTML entities
        var decoded = System.Net.WebUtility.HtmlDecode(noCitations);
        // Remove any remaining square brackets that might not have been caught
        var cleanText = System.Text.RegularExpressions.Regex.Replace(decoded, @"\[\d+\]", string.Empty);
        return cleanText;
    }
}
