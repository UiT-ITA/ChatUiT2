using ChatUiT2.Models;
using HtmlAgilityPack;
using MudBlazor;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using System.Text.Json;
using static System.Net.WebRequestMethods;
using Tiktoken.Encodings;
using System.Text.RegularExpressions;
using System.Reflection.Metadata;
using System.Text;

namespace ChatUiT2.Tools;

public static class ChatTools
{

    private static ChatTool getCurrentLocationTool = ChatTool.CreateFunctionTool(
        functionName: "getCurrentLocation",
        functionDescription: "Get the current location of the user"
    );

    private static ChatTool getCurrentDateTimeTool = ChatTool.CreateFunctionTool(
        functionName: "getCurrentDateTime",
        functionDescription: "Get the current date and time"
    );

    private static ChatTool getWeatherTool = ChatTool.CreateFunctionTool(
        functionName: "getWeather",
        functionDescription: "Get the weather for a given location and date",
        functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "location": {
                        "type": "string",
                        "description": "The city and state, e.g. Boston, MA"
                    },
                    "unit": {
                        "type": "string",
                        "enum": [ "celsius", "fahrenheit" ],
                        "description": "The temperature unit to use. Infer this from the specified location."
                    }
                },
                "required": [ "location" ]
            }
            """)
    );

    private static ChatTool getWikiEntryTool = ChatTool.CreateFunctionTool(
        functionName: "GetWikipediaEntry",
        functionDescription: "Get the first section of a Wikipedia article",
        functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "topic": {
                        "type": "string",
                        "description": "The topic to search for on Wikipedia"
                    }
                },
                "required": [ "topic" ]
            }
            """)
    );

    private static ChatTool getWebpageTool = ChatTool.CreateFunctionTool(
        functionName: "GetWebpage",
        functionDescription: "Get the content of a webpage",
        functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "url": {
                        "type": "string",
                        "description": "The URL of the webpage to fetch"
                    }
                },
                "required": [ "url" ]
            }
            """)
    );

    public static List<ChatToolDescription> Tools = new List<ChatToolDescription>
    {
        new ChatToolDescription
        {
            DisplayName = "Location",
            Description = "Allow the AI to access your location if needed",
            Icon = Icons.Material.Filled.LocationOn,
            Tool = getCurrentLocationTool,
        },
        new ChatToolDescription
        {
            DisplayName = "DateTime",
            Description = "Get the current date and time",
            Icon = Icons.Material.Filled.Schedule,
            Tool = getCurrentDateTimeTool
        },
        new ChatToolDescription
        {
            DisplayName = "Weather",
            Description = "Get the weather for a location",
            Icon = Icons.Material.Filled.WbSunny,
            Tool = getWeatherTool
        },
        new ChatToolDescription
        {
            DisplayName = "Wikipedia",
            Description = "Get the first section and the infobox of a Wikipedia article",
            Icon = Icons.Material.Filled.MenuBook,
            Tool = getWikiEntryTool
        },
        new ChatToolDescription
        {
            DisplayName = "Webpage",
            Description = "Get the content of a webpage",
            Icon = Icons.Material.Filled.Web,
            Tool = getWebpageTool
        }
    };

    public static string GetLocation()
    {
        Console.WriteLine("GetLocation");
        return "Tromsø, NO";
    }

    public static string GetDateTime()
    {
        Console.WriteLine("GetDateTime");
        // Get the current date (date, month, year) and time (hours, minutes) as string. ex: 18.10.2021 14:30
        return DateTime.Now.ToString("dd.MM.yyyy HH:mm");
    }

    public static string GetWeather(string location, string unit = "celsius")
    {
        Console.WriteLine($"GetWeather: {location}, {unit}");
        // Get the weather for the given location and date
        return "The weather in " + location + " is 20°C";
    }

    public static string GenerateImage(string description)
    {
        Console.WriteLine($"GenerateImage: {description}");
        return "Not implemented";
    }

    public static async Task<string> GetWikipedia(string topic)
    {
        Console.WriteLine($"GetWikipedia: {topic}");
        return await WikipeidaHelper.GetWikipediaSectionAsync(topic);
    }

    public static async Task<string> GetWebpage(string url)
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

    private static string TrimContent(string content, int tokens = 20_000, ModelName model = ModelName.gpt4omini)
    {

        var content1 = StripHTML(content);

        content = content1;

        Tiktoken.Encoder encoder;
        
        if (model == ModelName.gpt4omini)
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

    private static string StripHTML(string htmlContent)
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


    public static async Task<string> HandleToolCall(ChatToolCall toolCall)
    {
        try
        {
            using JsonDocument argumentsDocument = JsonDocument.Parse(toolCall.FunctionArguments);
            switch (toolCall.FunctionName)
            {
                case "getCurrentLocation":
                    return GetLocation();
                case "getCurrentDateTime":
                    return GetDateTime();
                case "getWeather":
                    if (!argumentsDocument.RootElement.TryGetProperty("location", out JsonElement locationElement))
                    {
                        return "This tool needs a valid location";
                    }
                    else
                    {
                        string location = locationElement.GetString()!;
                        if (argumentsDocument.RootElement.TryGetProperty("unit", out JsonElement unitElement))
                        {
                            return GetWeather(location, unitElement.GetString()!);
                        }
                        else
                        {
                            return GetWeather(location);
                        }
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
    public static async Task<string> GenerateImageAsync(string description)
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
    public static async Task<string> GetWikipediaSectionAsync(string topic, string? section = null)
    {
        HttpClient client = new HttpClient();
        try
        {
            string url = $"https://en.wikipedia.org/w/api.php?action=parse&page={Uri.EscapeDataString(topic)}&prop=text|sections&format=json";
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
                return await GetWikipediaSectionAsync(redirectTopic);
            }

            if (string.IsNullOrEmpty(section))
            {
                var sectionArray = json["parse"]?["sections"] as JArray;
                string? firstSection = ExtractFirstSection(html);
                string? infobox = ExtractInfobox(html);
                string? sections = ExtractSections(sectionArray);
                Console.WriteLine($"Sections: {sections}");

                if (firstSection == null)
                {
                    return "Topic not found";
                }

                return firstSection + "Facts:\n" + infobox + "\n\nSections for further reading:\n" + sections;
            }
            else
            {
                //Please handle this here
                var sectionContent = ExtractSection(html, section);
                return sectionContent ?? "Section or topic not found";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return "Topic not found";
        }
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

    private static string? ExtractSections(JArray? sections)
    {
        if (sections == null) return null;

        var skipSections = new[] { "Notes", "References", "Further reading", "External links", "Bibliography" };
        var result = new StringBuilder();

        foreach (var section in sections)
        {
            string line = section["line"]?.ToString() ?? "";
            if (skipSections.Contains(line)) continue;

            string number = section["number"]?.ToString() ?? "";
            string anchor = section["anchor"]?.ToString() ?? "";
            int level = int.Parse(section["toclevel"]?.ToString() ?? "1");

            string indent = new string(' ', (level - 1) * 2);
            result.AppendLine($"{indent}{number}: {line} (anchor: {anchor})");
        }

        return result.ToString().TrimEnd();
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

    private static string? ExtractSection(string html, string anchor)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        // Find section heading by id
        var sectionNode = document.DocumentNode.SelectSingleNode($"//h2[@id='{anchor}'] | //h3[@id='{anchor}'] | //h4[@id='{anchor}']");
        if (sectionNode == null) return null;

        var content = new StringBuilder();
        var node = sectionNode.NextSibling;
        var headingLevel = int.Parse(sectionNode.Name.Substring(1));

        while (node != null)
        {
            if (node.Name.StartsWith("h") && int.Parse(node.Name.Substring(1)) <= headingLevel)
                break;

            if (node.Name == "p" || node.Name == "ul" || node.Name == "ol")
            {
                content.AppendLine(StripHtmlTags(node.OuterHtml));
            }
            node = node.NextSibling;
        }

        return content.ToString().Trim();
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
