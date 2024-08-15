using static System.Net.WebRequestMethods;

namespace ChatUiT2.Tools;

public class ChatTools
{
    public static List<Dictionary<string, object>> GetTools()
    {
        return new List<Dictionary<string, object>>
        {
            new Dictionary<string, object>
            {
                { "name", "generate_image" },
                { "description", "Generate an image based on the provided description." },
                { "parameters", new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>
                            {
                                { "description", new Dictionary<string, object>
                                    {
                                        { "type", "string" },
                                        { "description", "The description of the image to generate." }
                                    }
                                }
                            }
                        },
                        { "required", new List<string> { "description" } },
                        { "additionalProperties", false }
                    }
                }
            }
        };
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
