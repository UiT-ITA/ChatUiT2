using ChatUiT2.Tools;

namespace ChatUiT2.Models;

public class ChatFile
{
    public string FileName { get; set; } = "";
    public FileType FileType {
        get
        {
            return FileTools.GetFileTypeFromName(FileName);
        }
    }
    public byte[]? Bytes { get; set; }

}

public enum FileType
{
    Image,
    Text,
    Composite
}
