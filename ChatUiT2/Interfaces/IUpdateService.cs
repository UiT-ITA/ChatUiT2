namespace ChatUiT2.Interfaces;

public interface IUpdateService
{
    event Action? OnChatUpdate;
    event Action? OnGlobalUpdate;

    void Update(UpdateType type);
}   
public enum UpdateType
{
    ChatMessage,
    Global,
    All
}