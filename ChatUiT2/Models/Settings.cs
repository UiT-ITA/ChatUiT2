namespace ChatUiT2.Models;

public class Settings
{

}

public class Config
{
    // TODO: Fix
    //public List<Model> Models { get; set; } = new Model[]
    //{
    //    new Model { Name = "GPT-3.5-Turbo", Type = ModelType.Chat, MaxContext = 16000, MaxTokens = 4096 },
    //    new Model { Name = "GPT-4-Turbo", Type = ModelType.Chat, MaxContext = 128000, MaxTokens = 4096 },
    //    new Model { Name = "GPT-4o", Type = ModelType.MultiModal, MaxContext = 128000, MaxTokens = 4096 },
    //    new Model { Name = "DALL-E", Type = ModelType.Image, MaxContext = 1280, MaxTokens = 1280 }
    //}.ToList();
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