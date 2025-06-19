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
    
    private static ChatTool getPersonalhandbokDataTool = ChatTool.CreateFunctionTool(
        functionName: "getPersonalhandbok",
        functionDescription: "Get documentation from personalhandbok at UiT",
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

    public static List<string> ImageStyles = new List<string>
    {
        "natural",
        "vivid"
    };

    public static List<string> ImageSizes = new List<string>
    {
        "square",
        "portrait",
        "landscape"
    };


    private static ChatTool getImageTool = ChatTool.CreateFunctionTool(
        functionName: "GetImageGeneration",
        functionDescription: "Generate images",
        functionParameters: BinaryData.FromString($$"""
            {
                "type": "object",
                "properties": {
                    "description": {
                        "type": "string",
                        "description": "Description of image to generate. Make it as descriptive as possible."
                    },
                    "style": {
                    "type": "string",
                    "enum": [ {{string.Join(", ", ChatTools.ImageStyles.Select(style => $"\"{style}\""))}} ],
                    "description": "Style of the image. Use vivid to create more hyper-real or cinematic images. Default is {{ImageStyles[0]}}."
                    },
                    "size": {
                    "type": "string",
                    "enum": [ {{string.Join(", ", ChatTools.ImageSizes.Select(size => $"\"{size}\""))}} ],
                    "description": "Aspect ratio of iamge: square(1024x1024), portrait(1792x1024) or landscape(1024x1792). Default is {{ImageSizes[0]}}."
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
            DisplayName = "Personalhandbok",
            Description = "Get documentation from Personalhandbok",
            Icon = Icons.Material.Filled.WbSunny,
            Tool = getPersonalhandbokDataTool
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
