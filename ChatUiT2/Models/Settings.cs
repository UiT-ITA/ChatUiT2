namespace ChatUiT2.Models;

public class Settings
{

}

public class AppConfig
{
    public List<Model> Models { get; set; } = null!;
    public Model DefaultModel { get; set; } = null!;
    public Model NameingModel { get; set; } = null!;


}


public class Preferences
{
    public bool DarkMode { get; set; } = true;
    public bool SaveHistory { get; set; } = true;
    public string Language { get; set; } = "en";
    public ChatSettings DefaultChatSettings { get; set; } = new ChatSettings();
}

public class ChatSettings
{
    public string Model { get; set; } = "GPT-4-Turbo";
    public float Temperature { get; set; } = 0.2f;
    public string Prompt { get; set; } = "You are a helpful ai assistant, respond using markdown.";
    public int MaxTokens { get; set; } = 1024;


    public void Copy(ChatSettings settings)
    {
        Model = settings.Model;
        Temperature = settings.Temperature;
        Prompt = settings.Prompt;
        MaxTokens = settings.MaxTokens;
    }
}