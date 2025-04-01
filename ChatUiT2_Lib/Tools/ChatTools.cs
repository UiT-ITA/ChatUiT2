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

    private static ChatTool getImageTool = ChatTool.CreateFunctionTool(
        functionName: "GetImageGeneration",
        functionDescription: "Generate images",
        functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "description": {
                        "type": "string",
                        "description": "Description of image to generate"
                    },
                    "style": {
                    "type": "string",
                    "enum": [ "natural", "vivid" ],
                    "description": "Style of the image, can be vivid or natural"
                    },
                    "size": {
                    "type": "string",
                    "enum": [ "square", "portrait", "landscape" ],
                    "description": "Size of images. Must be square(1024x1024), portrait(1792x1024) or landscape(1024x1792)"
                    },
                    "quality": {
                    "type": "string",
                    "enum": ["hd", "standard"],
                    "description": "Quality of image. Should always be hd"
                    }
                },
                "required": [ "description" ]
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
        },
        new ChatToolDescription
        {
            DisplayName = "ImageGeneration",
            Description = "Generate images",
            Icon = Icons.Material.Filled.Image,
            Tool = getImageTool
        }
    };
}
