using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System.Xml;

namespace ChatUiT2.Services;



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
            string html = json["parse"]["text"]["*"].ToString();
            string firstSection = ExtractFirstSection(html);
            string infobox = ExtractInfobox(html);

            return firstSection + "Facts:\n" + infobox;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return string.Empty;
        }
    }

    private static string ExtractFirstSection(string html)
    {
        // Find the end of the infobox
        var infoboxEnd = html.IndexOf("</table>");
        if (infoboxEnd == -1)
        {
            Console.WriteLine("Infobox end not found.");
            return null;
        }
        // Find the end of the first section using the <meta property="mw:PageProp/toc"> tag
        var firstSectionEnd = html.IndexOf("<h2", infoboxEnd);
        if (firstSectionEnd == -1)
        {
            Console.WriteLine("First section end not found.");
            return null;
        }
        // Extract the HTML for the first section
        string firstSectionHtml = html.Substring(infoboxEnd + 8, firstSectionEnd - infoboxEnd - 8);
        // Strip HTML tags to get plain text
        string plainText = StripHtmlTags(firstSectionHtml);
        // Debug output
        Console.WriteLine("Extracted First Section:");
        Console.WriteLine(plainText);
        return plainText;
    }

    private static string ExtractInfobox(string html)
    {
        var infoboxStart = html.IndexOf("infobox");
        if (infoboxStart == -1) return null;
        var infoboxEnd = html.IndexOf("</table>", infoboxStart);
        if (infoboxEnd == -1) return null;
        string infoboxHtml = html.Substring(infoboxStart - 10, infoboxEnd - infoboxStart + 10);
        //return infoboxHtml;
        return ExtractAndFormatInfobox(infoboxHtml);
    }

    private static string ExtractAndFormatInfobox(string infoboxHtml)
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

public static class ChatToolService
{
    public static async Task<string> GetWikipediaEntry(string topic)
    {
        return await WikipeidaHelper.GetWikipediaFirstSectionAsync(topic);
    }

}