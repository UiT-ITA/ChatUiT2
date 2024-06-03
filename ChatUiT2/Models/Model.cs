namespace ChatUiT2.Models;

public class Model
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string DeploymentName { get; set; } = "";
    public string ModelType { get; set; } = "";
    public string Deployment { get; set; } = "";
    public int MaxContext { get; set; } = 4096;
    public int MaxTokens { get; set; } = 4096;
}


public enum ModelType
{
    Chat,
    Image,
    MultiModal
}

public enum ModelService
{
    AzureOpenAI,
    Custom
}
