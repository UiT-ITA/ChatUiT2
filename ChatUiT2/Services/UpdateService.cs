using ChatUiT2.Interfaces;

namespace ChatUiT2.Services;

public class UpdateService: IUpdateService
{
    public event Action? OnInputUpdate;
    public event Action? OnChatMessageUpdate;
    public event Action? OnWorkItemUpdate;
    public event Action? OnGlobalUpdate;

    public void Update(UpdateType type)
    {
        if (type == UpdateType.Input)
        {
            OnInputUpdate?.Invoke();
        }
        else if (type == UpdateType.ChatMessage)
        {
            OnChatMessageUpdate?.Invoke();
        }
        else if (type == UpdateType.WorkItem)
        {
            OnWorkItemUpdate?.Invoke();
        }
        else if (type == UpdateType.Global)
        {
            OnGlobalUpdate?.Invoke();
        }
        else if (type == UpdateType.All)
        {
            OnInputUpdate?.Invoke();
            OnChatMessageUpdate?.Invoke();
            OnWorkItemUpdate?.Invoke();
            OnGlobalUpdate?.Invoke();
        }
        else
        {
            throw new NotImplementedException();
        }
    }
}


