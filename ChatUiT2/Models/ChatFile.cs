using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ChatUiT2.Models;

public class ChatFile
{
    public string FileName { get; set; } = "";
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

    public static bool VerifyFile(ChatFile file)
    {
        if (!AllFiles.Contains(file.FileName.Split('.').Last()))
        {
            Console.WriteLine("File type not supported.");
            return false;
        }

        if (file.Bytes == null)
        {
            Console.WriteLine("File is empty.");
            return false;
        }

        if (file.FileType == FileType.Image)
        {
            try
            {
                using (var stream = new MemoryStream(file.Bytes))
                {
                    using (var image = Image.Load<Rgba32>(stream))
                    {
                        // Image successfully loaded, not corrupted
                    }
                }
            }
            catch
            {
                // Handle the corrupted image case
                Console.WriteLine("The image is corrupted.");
                return false;
            }
        }
        else if (file.FileType == FileType.Data)
        {
            // Verify data
        }
        else if (file.FileType == FileType.Document)
        {
            // Verify document
        }

        return true;
    }
}

public enum FileType
{
    Image,
    Data,
    Document
}

