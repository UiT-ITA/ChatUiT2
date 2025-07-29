using ChatUiT2.Interfaces;

namespace ChatUiT2.Services;

public class UpdateService: IUpdateService
{
    public event Action? OnChatUpdate;
    public event Action? OnGlobalUpdate;

    public UpdateService()
    {
    }

    public void Update(UpdateType type)
    {
        if (type == UpdateType.ChatMessage)
        {
            OnChatUpdate?.Invoke();
        }
        else if (type == UpdateType.Global)
        {
            OnGlobalUpdate?.Invoke();
        }
        else if (type == UpdateType.All)
        {
            OnChatUpdate?.Invoke();
            OnGlobalUpdate?.Invoke();
        }
        else
        {
            throw new NotImplementedException();
        }
    }
}


