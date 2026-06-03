namespace ChatUiT2.Models;

public class Settings
{

}


public class Preferences
{
    public bool DarkMode { get; set; } = true;
    public bool SaveHistory { get; set; } = true;
    public bool UseMarkdown { get; set; } = true;
    public bool SmoothOutput { get; set; } = false;
    public ChatWidth ChatWidth { get; set; } = ChatWidth.Medium;
    public string Language { get; set; } = "en";
    public ChatSettings DefaultChatSettings { get; set; } = new ChatSettings();
}

public class ChatSettings
{
    public string Model { get; set; } = "GPT-5-Mini";
    public float Temperature { get; set; } = 0.2f;
    public string Prompt { get; set; } = "You are a helpful ai assistant, respond using markdown.";
    public int MaxTokens { get; set; } = 16_384;


    public void Copy(ChatSettings settings)
    {
        // Model is intentionally NOT copied: the model is not a saved user preference.
        // A new chat always gets the system default model (see UserService.NewChat),
        // so it must not be carried through DefaultChatSettings.
        Temperature = settings.Temperature;
        Prompt = settings.Prompt;
        MaxTokens = settings.MaxTokens;
    }
}


public enum ChatWidth
{
    Small,
    Medium,
    Large,
    Full
};