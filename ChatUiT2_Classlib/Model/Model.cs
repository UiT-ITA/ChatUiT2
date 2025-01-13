namespace ChatUiT2.Models;

public class Model
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string DeploymentName { get; set; } = null!;
    public string DeploymentType { get; set; } = null!;
    public string Deployment { get; set; } = null!;
    public int MaxContext { get; set; }
    public int MaxTokens { get; set; }
}

public class ModelEndpoint
{
    public string Name { get; set; } = null!;
    public string Url { get; set; } = null!;
    public string Key { get; set; } = null!;
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
