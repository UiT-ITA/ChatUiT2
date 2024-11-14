﻿using ChatUiT2.Tools;
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
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string FileName { get; set; }
    public FileType FileType { get; set; }
    public List<ChatFilePart> Parts { get; set; }


    public ChatFile(string fileName, byte[] data)
    {
        FileName = fileName;
        FileType = FileTools.GetFileTypeFromName(fileName);
        Parts = FileTools.ProcessFile(FileType, data);
    }

}

public class ChatFilePart
{
    public FilePartType Type { get; set; }
    public string Data { get; set; } = null!;
}

public class TextFilePart : ChatFilePart
{
    public TextFilePart(string text)
    {
        Type = FilePartType.Text;
        Data = text;
    }
}

public class ImageFilePart : ChatFilePart
{
    public int Width { get; set; }
    public int Height { get; set; }
    public ImageFilePart(string data, int width, int height)
    {
        Type = FilePartType.Image;
        Data = data;
        Width = width;
        Height = height;
    }

    public ImageFilePart(string data)
    {
        Type = FilePartType.Image;
        Data = data;
        (Width, Height) = FileTools.GetImageDimensions(data);
    }

    public ImageFilePart(byte[] data)
    {
        Type = FilePartType.Image;
        Data = FileTools.ImageToBase64(data);
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
