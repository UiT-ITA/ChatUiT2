using ChatUiT2.Tools;

namespace ChatUiT2.Models;

public class Instruction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public bool IsFavorite { get; set; } = false;
    public InstructionContent Content { get; set; } = new InstructionContent();
}

public class InstructionContent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Owner { get; set; } = "";
    public string Name { get; set; } = "New Instruction";
    public string Description { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Created { get; set; } = DateTimeTools.GetTimestamp();
    public List<string> SharedWith { get; set; } = new List<string>();
}
