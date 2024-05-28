namespace ChatUiT2.Models;

public class Model
{
    public string Name { get; set; }
    public ModelType Type { get; set; }
    public int MaxContext { get; set; }
    public int MaxTokens { get; set; }
}

public enum ModelType
{
    Chat,
    Image,
    MultiModal
}
