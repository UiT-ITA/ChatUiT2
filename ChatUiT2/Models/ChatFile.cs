using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ChatUiT2.Models;

public class ChatFile
{
    public string FileName { get; set; } = "";
    public FileType FileType {
        get
        {
            return GetFileTypeFromName(FileName);
        }
    }
    public byte[]? Bytes { get; set; }

    public static string GetMimeType(ChatFile file)
    {
        if (file.FileType == FileType.Image)
        {
            string extention = file.FileName.Split('.').Last();
            if (extention == "png")
            {
                return "image/png";
            }
            else if(extention == "jpg" || extention == "jpeg")
            {
                return "image/jpeg";
            }
            else
            {
                throw new Exception("Unsupported image type: " + extention);
            }
        }
        else
        {
            return "text/plain";
        }
    }

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

    // TODO: add support for more files
    public static List<string> ImageFiles = new() { "png", "jpg", "jpeg" };
    public static List<string> DataFiles = new() { /*"xlsx", "csv", "json" */};
    public static List<string> DocumentFiles = new() { "txt", /*"pdf", "docx"*/};
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

    public static string GetText(ChatFile file)
    {
        if (file.FileType == FileType.Data)
        {
            return "This is a data file.";
        }
        else if (file.FileType == FileType.Document)
        {
            if (file.FileName.Split('.').Last() == "txt")
            {
                return System.Text.Encoding.UTF8.GetString(file.Bytes!);
            }
            else
            {
                throw new Exception("Unsupported document type: " + file.FileName.Split('.').Last());
            }
        }
        else
        {
            throw new Exception("Unsupported file type.");
        }
    }
}

public enum FileType
{
    Image,
    Data,
    Document
}

