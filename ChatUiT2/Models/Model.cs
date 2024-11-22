using MudBlazor;

namespace ChatUiT2.Models;

//public class Model
//{
//    public string Name { get; set; } = null!;
//    public string Description { get; set; } = null!;
//    public string DeploymentName { get; set; } = null!;
//    public string DeploymentType { get; set; } = null!;
//    public string Deployment { get; set; } = null!;
//    public int MaxContext { get; set; }
//    public int MaxTokens { get; set; }
//}

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




public class Model()
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DisplayName { get; set; } = "Model display name";
    public string ModelName { get; set; } = null!;
    public string Description { get; set; } = "";
    public string Icon { get; set; } = Icons.Material.Filled.Star;
    public ModelService Service { get; set; } = ModelService.AzureOpenAI;
    public bool Images { get; set; } = false;
    public bool Audio { get; set; } = false;
    public bool Video { get; set; } = false; // No functionallity as of now to support this
    public int MaxContext { get; set; } = 128000;
    public int MaxTokens { get; set; } = 4096;
    public Endpoint Endpoint { get; set; } = null!;
    public List<string> Groups { get; set; } = new List<string>();

}

public class  Endpoint()
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Endpoint display name";
    public string Url { get; set; } = null!;
    public string Key { get; set; } = null!;
}
