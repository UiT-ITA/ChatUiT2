namespace ChatUiT2.Models;

public class ChatFile
{
    public string FileName { get; set; }
    public FileType FileType { get; set; }
    public byte[]? Bytes { get; set; }

    public static FileType GetFileTypeFromName(string name)
    {
        string extention = name.Split('.').Last();
        if (ImageFiles.Contains(extention))
        {
            return FileType.Image;
        }
        else if (DataFiles.Contains(extention))
        {
            return FileType.Data;
        }
        else if (DocumentFiles.Contains(extention))
        {
            return FileType.Document;
        }
        else
        {
            return FileType.Document;
        }
    }

    public static List<string> ImageFiles = new() { "jpg", "png", "bmp" };
    public static List<string> DataFiles = new() { "xlsx", "csv", "json" };
    public static List<string> DocumentFiles = new() { "pdf", "txt", "docx"};
    public static List<string> AllFiles = ImageFiles.Concat(DataFiles).Concat(DocumentFiles).ToList();
}

public enum FileType
{
    Image,
    Data,
    Document
}

