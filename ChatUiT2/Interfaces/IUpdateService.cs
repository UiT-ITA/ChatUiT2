namespace ChatUiT2.Interfaces;

public interface IUpdateService
{
    event Action? OnInputUpdate;
    event Action? OnChatMessageUpdate;
    event Action? OnWorkItemUpdate;
    event Action? OnGlobalUpdate;

    void Update(UpdateType type);
}   
public enum UpdateType
{
    Input,
    ChatMessage,
    WorkItem,
    Global,
    All
}