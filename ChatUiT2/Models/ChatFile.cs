using ChatUiT2.Tools;
using iText.Commons.Utils;

namespace ChatUiT2.Models;

//public class ChatFileOld
//{
//    public string FileName { get; set; } = "";
//    public FileTypeOld FileType {
//        get
//        {
//            return FileTools.GetFileTypeFromName(FileName);
//        }
//    }
//    public byte[]? Bytes { get; set; }

//}

public class ChatFile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; }
    public FileType FileType { get; set; }
    public List<ChatFilePart> Parts { get; set; }


    public ChatFile(string fileName, byte[] data)
    {
        FileName = fileName;
        FileType = FileTools.GetFileTypeFromName(fileName);
        Parts = FileTools.ProcessFile(FileType, data);
    }
    public ChatFile(string id, string fileName, List<ChatFilePart> parts)
    {
        Id = id;
        FileName = fileName;
        FileType = FileTools.GetFileTypeFromName(fileName);
        Parts = parts;
    }

}
public class ChatFilePart
{
    public FilePartType Type { get; set; }
}

public class TextFilePart : ChatFilePart
{
    public string Data { get; set; }
    public TextFilePart(string text)
    {
        Type = FilePartType.Text;
        Data = text;
    }
}

public class ImageFilePart : ChatFilePart
{
    public byte[] Data { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }


    public ImageFilePart(byte[] data)
    {
        Type = FilePartType.Image;
        Data = data;
        (Width, Height) = FileTools.GetImageDimensions(data);
    }
}

public enum FileTypeOld
{
    Text,
    Image,
    Composite
}

public enum FilePartType
{
    Text,
    Image
    //, Audio
}
