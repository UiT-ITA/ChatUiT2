using MudBlazor;
using OpenAI.Chat;

namespace ChatUiT2.Models;

public class ChatToolDescription
{
    public string DisplayName { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Icon { get; set; } = Icons.Material.Filled.Build;
    public ChatTool Tool { get; set; } = null!;
}
