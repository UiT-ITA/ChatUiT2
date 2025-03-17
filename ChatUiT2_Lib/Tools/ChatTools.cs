using ChatUiT2.Models;
using MudBlazor;
using OpenAI.Chat;

namespace ChatUiT2.Tools;

public class ChatTools
{
    private static ChatTool getTopdeskDataTool = ChatTool.CreateFunctionTool(
        functionName: "getTopdesk",
        functionDescription: "Get documentation from IT-support at UiT",
        functionParameters: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "query": {
                            "type": "string",
                            "description": "The isolated question to search the database for. Question must be formulated in norwegian"
                        }
                    },
                    "required": [ "query" ]
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
    public static List<ChatToolDescription> Tools { get; set; } = new List<ChatToolDescription>
    {
        new ChatToolDescription
        {
            DisplayName = "Topdesk",
            Description = "Get documentation from Orakelet",
            Icon = Icons.Material.Filled.WbSunny,
            Tool = getTopdeskDataTool
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
}
