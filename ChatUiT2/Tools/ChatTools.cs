using ChatUiT2.Models;
using HtmlAgilityPack;
using MudBlazor;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using System.Text.Json;
using static System.Net.WebRequestMethods;

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
        return await WikipeidaHelper.GetWikipediaFirstSectionAsync(topic);
    }

    public static async Task<string> GetWebpage(string url)
    {
        Console.WriteLine($"GetWebpage: {url}");

        HttpClient client = new HttpClient();
        try
        {
            string response = await client.GetStringAsync(url);
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return "An error occurred while fetching the webpage";
        }
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
