using OpenAI.Chat;

namespace ChatUiT2.Models;

public class Assistant
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = null!;
    public string Description { get; set; } = String.Empty;
    public AiModel Model { get; set; } = null!;
    public string SystemPrompt { get; set; } = String.Empty;
    public float Temperature { get; set; } = 0.2f;
    public AssistantReasoningLevel? ReasoningLevel { get; set; } = AssistantReasoningLevel.None; // Only used for reasoning models

    public List<ChatToolDescription> Tools { get; set; } = new List<ChatToolDescription>();

    public string Owner { get; set; } = null!;
    public List<UserRole> AllowedRoles { get; set; } = new List<UserRole>();
    public List<string> AllowedUsers { get; set; } = new List<string>();

    public bool VisiblePrompt { get; set; } = true;

}

public enum AssistantReasoningLevel
{
    None,
    Low,
    Medium,
    High
}
