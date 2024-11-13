﻿using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Xobject;
using iText.Kernel.Pdf;
using iText.Commons.Utils;
using iText.IO.Image;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;
using iText.Kernel.Geom;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ChatUiT2.Models;
using DocumentFormat.OpenXml.Vml;
using OpenAI.Chat;
using SharpCompress.Common;
using System.Drawing;
using SkiaSharp;
using static iText.IO.Codec.TiffWriter;

namespace ChatUiT2.Tools;

public static class FileTools
{
    public static string GetAcceptedFilesString()
    {
        // Dynamically create a string with all filetypes that are accepted in the form ".txt,.pdf,...." based on filetypes in the FileType enum, excluding "other"
        StringBuilder sb = new();
        foreach (var type in Enum.GetValues<FileType>())
        {
            if (type != FileType.other)
            {
                sb.Append($".{type},");
            }
        }

        return sb.ToString().TrimEnd(',');
    }

    public static bool IsImage(FileType type)
    {
        return type switch
        {
            FileType.bmp => true,
            FileType.jpg => true,
            FileType.jpeg => true,
            FileType.png => true,
            _ => false,
        };
    }

    private static readonly Dictionary<FileType, Func<byte[], List<ChatFilePart>>> _fileHnadlers = new()
    {
        { FileType.json, HandleTextFile },
        { FileType.txt, HandleTextFile },
        { FileType.csv, HandleTextFile },
        { FileType.html, HandleTextFile },

        { FileType.bmp, HandleImageFile },
        { FileType.jpg, HandleImageFile },
        { FileType.jpeg, HandleImageFile },
        { FileType.png, HandleImageFile },

        { FileType.docx, HandleDocxFile },
        { FileType.pdf, HandlePdfFile },

        { FileType.other, HandleTextFile }
    };

    public static List<ChatFilePart> ProcessFile(FileType type, byte[] data)
    {
        if (_fileHnadlers.TryGetValue(type, out var handler))
        {
            return handler(data);
        }
        else
        {
            throw new NotImplementedException("File type not supported");
        }
    }

    private static List<ChatFilePart> HandleTextFile(byte[] data)
    {
        string text = Encoding.UTF8.GetString(data);
        return new List<ChatFilePart> { new TextFilePart(text) };
    }

    private static List<ChatFilePart> HandleImageFile(byte[] data)
    {
        string base64 = Convert.ToBase64String(data);
        return new List<ChatFilePart> { new ImageFilePart(base64) };
    }

    private static List<ChatFilePart> HandleDocxFile(byte[] data)
    {
        throw new NotImplementedException();
    }

    private static List<ChatFilePart> HandlePdfFile(byte[] data)
    {
        throw new NotImplementedException();
    }

    public static FileType GetFileTypeFromName(string name)
    {
        string extention = name.Split('.').Last();
        if (Enum.TryParse<FileType>(extention, true, out var type))
        {
            return type;
        }
        else
        {
            return FileType.other;
        }
    }


    private static List<ChatFilePart> ExtractContentFromPdf(byte[] data, int imageSizeCutof = 10_000)
    {
        List<ChatFilePart> content = new ();

        if (data == null)
        {
            throw new Exception("File is empty");
        }

        try
        {
            using (PdfReader pdfReader = new PdfReader(new MemoryStream(data)))
            using (PdfDocument pdfDocument = new PdfDocument(pdfReader))
            {
                int numberOfPages = pdfDocument.GetNumberOfPages();
                for (int page = 1; page <= numberOfPages; page++)
                {
                    var strategy = new CustomTextExtractionStrategy();
                    PdfPage pdfPage = pdfDocument.GetPage(page);
                    if (pdfPage == null)
                    {
                        continue;
                    }
                    string text = PdfTextExtractor.GetTextFromPage(pdfPage, strategy);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        content.Add(new TextFilePart(text));
                    }
                    PdfResources resources = pdfPage.GetResources();
                    if (resources == null)
                    {
                        continue;
                    }
                    var resourceNames = resources.GetResourceNames(PdfName.XObject);
                    if (resourceNames == null)
                    {
                        continue;
                    }
                    foreach (PdfName key in resourceNames)
                    {
                        PdfObject obj = resources.GetResourceObject(PdfName.XObject, key);
                        if (obj == null)
                        {
                            continue;
                        }
                        if (obj is PdfStream stream)
                        {
                            PdfImageXObject? image = null;
                            try
                            {
                                image = new PdfImageXObject(stream);
                            }
                            catch
                            {
                                continue;
                            }
                            byte[]? imageData = null;
                            try
                            {
                                imageData = image.GetImageBytes(true);
                            }
                            catch
                            {
                                continue;
                            }
                            if (imageData != null && imageData.Length > imageSizeCutof)
                            {
                                content.Add(new ImageFilePart(imageData));
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            throw;
        }
        return content;
    }


    private static List<ChatFilePart> ExtractContentFromDocx(byte[] data, int imageSizeCutof = 10_000)
    {
        List<ChatFilePart> content = new();

        if (data == null)
        {
            throw new Exception("File is empty");
        }

        using (MemoryStream memoryStream = new MemoryStream(data))
        using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(memoryStream, false))
        {
            if (wordDoc.MainDocumentPart == null || wordDoc.MainDocumentPart.Document.Body == null)
            {
                return content;
            }
            Body body = wordDoc.MainDocumentPart.Document.Body;
            foreach (var element in body.Elements())
            {
                if (element is Paragraph paragraph)
                {
                    foreach (var paragraphElement in paragraph.Elements())
                    {
                        if (paragraphElement is Run run)
                        {
                            foreach (var runElement in run.Elements())
                            {
                                if (runElement is Text textElement)
                                {
                                    content.Add(new TextFilePart(textElement.Text));
                                }
                                else if (runElement is Break or LastRenderedPageBreak)
                                {
                                    content.Add(new TextFilePart("\n"));
                                }
                                else if (runElement is Drawing drawing)
                                {
                                    var blip = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().FirstOrDefault();
                                    if (blip != null && blip.Embed?.Value != null)
                                    {
                                        string embed = blip.Embed.Value;
                                        var imagePart = (ImagePart)wordDoc.MainDocumentPart.GetPartById(embed);
                                        using (var stream = imagePart.GetStream())
                                        using (var memoryStreamImage = new MemoryStream())
                                        {
                                            stream.CopyTo(memoryStreamImage);
                                            byte[] imageData = memoryStreamImage.ToArray();
                                            if (imageData.Length > 10_000) // Used to filter out logos etc
                                            {
                                                content.Add(new ImageFilePart(imageData));
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    //Console.WriteLine("Unknown element: " + runElement.GetType().Name);
                                }
                            }
                        }
                        else if (paragraphElement is Hyperlink hyperlink)
                        {
                            var hyperlinkRel = wordDoc.MainDocumentPart.HyperlinkRelationships.FirstOrDefault(h => h.Id == hyperlink.Id);
                            var url = hyperlinkRel?.Uri.ToString() ?? string.Empty;
                            string displayText = hyperlink.InnerText;
                            content.Add(new TextFilePart($"[{displayText}]({url})"));
                        }
                        else if (paragraphElement is ParagraphProperties paragraphProperties)
                        {
                            if (paragraphProperties.NumberingProperties != null)
                            {
                                content.Add(new TextFilePart("* "));
                            }
                        }
                        else
                        {
                            //Console.WriteLine("Unknown element: " + paragraphElement.GetType().Name);
                        }
                    }
                    content.Add(new TextFilePart("\n\n"));
                }
            }
        }
        return content;
    }

    public static string ImageToBase64(byte[] imageBytes, bool resize = true)
    {
        using (var inputStream = new MemoryStream(imageBytes))
        using (var skBitmap = SKBitmap.Decode(inputStream))
        {
            SKBitmap bitmapToEncode = skBitmap;
            if (resize)
            {
                // Calculate new dimensions while maintaining aspect ratio
                int width = skBitmap.Width;
                int height = skBitmap.Height;
                if (width > 512 || height > 512)
                {
                    float aspectRatio = (float)width / height;
                    if (aspectRatio > 1) // Width is the longer side
                    {
                        width = 512;
                        height = (int)(512 / aspectRatio);
                    }
                    else // Height is the longer side
                    {
                        height = 512;
                        width = (int)(512 * aspectRatio);
                    }
                }
                bitmapToEncode = skBitmap.Resize(new SKImageInfo(width, height), SKFilterQuality.High);
            }
            using (var image = SKImage.FromBitmap(bitmapToEncode))
            using (var outputStream = new MemoryStream())
            {
                image.Encode(SKEncodedImageFormat.Png, 100).SaveTo(outputStream);
                // Return base64 string with included mime type
                var base64 = Convert.ToBase64String(outputStream.ToArray());
                return $"data:image/png;base64,{base64}";
            }
        }
    }
}

/*
public class FileToolsOld
{
    // TODO: Add support for more image formats, excel and word files
    public static List<string> ImageFiles = new() { "png", "jpg", "jpeg" };
    public static List<string> TextFiles = new() { "csv", "json", "txt"};
    public static List<string> CompositeFiles = new() { "pdf", "docx" };
    public static List<string> AllFiles = ImageFiles.Concat(TextFiles).Concat(CompositeFiles).ToList();

    public static bool VerifyFile(ChatFile file)
    {
        if (!AllFiles.Contains(file.FileName.Split('.').Last()))
        {
            //Console.WriteLine("File type not supported.");
            return false;
        }

        if (file.Bytes == null)
        {
            //Console.WriteLine("File is empty.");
            return false;
        }

        if (file.FileType == FileTypeOld.Image)
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
                //Console.WriteLine("The image is corrupted.");
                return false;
            }
        }
        else if (file.FileType == FileTypeOld.Text)
        {
            // Verify document

        }
        else if (file.FileType == FileTypeOld.Composite)
        {
            string extention = file.FileName.Split('.').Last();
            // Verify composite
            if (extention == "pdf")
            {
                try
                {
                    _ = ExtractContentFromPdf(file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to extract content from pdf");
                    Console.WriteLine(ex.Message);
                    return false;
                }
            }
            if (extention == "docx")
            {
                try
                {
                    _ = ExtractContentFromDocx(file);
                }
                catch (Exception)
                {
                    //Console.WriteLine("Failed to extract content from docx");
                    //Console.WriteLine(ex.Message);
                    return false;
                }
            }
        }

        return true;
    }
    
    public static string ExtractTextFromPdf(ChatFile file)
    {
        if (file.Bytes == null)
        {
            throw new Exception("File is empty");
        }

        using (PdfReader pdfReader = new PdfReader(new MemoryStream(file.Bytes)))
        using (PdfDocument pdfDocument = new PdfDocument(pdfReader))
        {
            StringWriter textWriter = new StringWriter();
            for (int page = 1; page <= pdfDocument.GetNumberOfPages(); page++)
            {
                //var strategy = new SimpleTextExtractionStrategy();
                var strategy = new CustomTextExtractionStrategy();
                string text = PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(page), strategy);
                textWriter.WriteLine(text);
            }
            return textWriter.ToString();
        }
    }

    public static string ExtractTextFromDocx(ChatFile file)
    {

        if (file.Bytes == null)
        {
            throw new Exception("File is empty");
        }

        using (MemoryStream memoryStream = new MemoryStream(file.Bytes))
        using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(memoryStream, false))
        {
            if (wordDoc.MainDocumentPart == null || wordDoc.MainDocumentPart.Document.Body == null)
            {
                return "";
            }
            Body body = wordDoc.MainDocumentPart.Document.Body;
            StringBuilder text = new StringBuilder();
            foreach (var element in body.Elements())
            {
                if (element is Paragraph paragraph)
                {
                    foreach (var paragraphElement in paragraph.Elements())
                    {
                        if (paragraphElement is Run run)
                        {
                            foreach (var runElement in run.Elements())
                            {
                                if (runElement is Text textElement)
                                {
                                    text.Append(textElement.Text);
                                }
                                else if (runElement is Break or LastRenderedPageBreak)
                                {
                                    text.Append("\n");
                                }
                                else
                                {
                                    //Console.WriteLine("Unknown element: " + runElement.GetType().Name);
                                }
                            }
                        }
                        else if (paragraphElement is Hyperlink hyperlink)
                        {
                            var hyperlinkRel = wordDoc.MainDocumentPart.HyperlinkRelationships.FirstOrDefault(h => h.Id == hyperlink.Id);
                            var url = hyperlinkRel?.Uri.ToString() ?? string.Empty;
                            string displayText = hyperlink.InnerText;
                            text.Append($"[{displayText}]({url})");
                        }
                        else if (paragraphElement is ParagraphProperties paragraphProperties)
                        {
                            if (paragraphProperties.NumberingProperties != null)
                            {
                                text.Append("* ");
                            }
                        }
                        else
                        {
                            //Console.WriteLine("Unknown element: " + paragraphElement.GetType().Name);
                        }
                    }
                    text.Append("\n");
                }
            }
            return text.ToString();
        }
    }

    public static List<FileContent> ExtractContentFromPdf(ChatFile file)
    {
        if (file.Bytes == null)
        {
            throw new Exception("File is empty");
        }
        List<FileContent> contents = new List<FileContent>();
        try
        {
            using (PdfReader pdfReader = new PdfReader(new MemoryStream(file.Bytes)))
            using (PdfDocument pdfDocument = new PdfDocument(pdfReader))
            {
                int numberOfPages = pdfDocument.GetNumberOfPages();
                for (int page = 1; page <= numberOfPages; page++)
                {
                    var strategy = new CustomTextExtractionStrategy();
                    PdfPage pdfPage = pdfDocument.GetPage(page);
                    if (pdfPage == null)
                    {
                        continue;
                    }
                    string text = PdfTextExtractor.GetTextFromPage(pdfPage, strategy);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        contents.Add(new FileText { Text = text });
                    }
                    PdfResources resources = pdfPage.GetResources();
                    if (resources == null)
                    {
                        continue;
                    }
                    var resourceNames = resources.GetResourceNames(PdfName.XObject);
                    if (resourceNames == null)
                    {
                        continue;
                    }
                    foreach (PdfName key in resourceNames)
                    {
                        PdfObject obj = resources.GetResourceObject(PdfName.XObject, key);
                        if (obj == null)
                        {
                            continue;
                        }
                        if (obj is PdfStream stream)
                        {
                            PdfImageXObject? image = null;
                            try
                            {
                                image = new PdfImageXObject(stream);
                            }
                            catch
                            {
                                continue;
                            }
                            byte[]? imageData = null;
                            try
                            {
                                imageData = image.GetImageBytes(true);
                            }
                            catch
                            {
                                continue;
                            }
                            if (imageData != null && imageData.Length > 10_000)
                            {
                                contents.Add(new FileImage { ImageData = imageData });
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            throw;
        }
        return contents;
    }

    public static List<FileContent> ExtractContentFromDocx(ChatFile file)
    {

        if (file.Bytes == null)
        {
            throw new Exception("File is empty");
        }
        List<FileContent> contents = new List<FileContent>();

        using (MemoryStream memoryStream = new MemoryStream(file.Bytes))
        using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(memoryStream, false))
        {
            if (wordDoc.MainDocumentPart == null || wordDoc.MainDocumentPart.Document.Body == null)
            {
                return contents;
            }
            Body body = wordDoc.MainDocumentPart.Document.Body;
            foreach (var element in body.Elements())
            {
                if (element is Paragraph paragraph)
                {
                    foreach (var paragraphElement in paragraph.Elements())
                    {
                        if (paragraphElement is Run run)
                        {
                            foreach (var runElement in run.Elements())
                            {
                                if (runElement is Text textElement)
                                {
                                    contents.Add(new FileText { Text = textElement.Text });
                                }
                                else if (runElement is Break or LastRenderedPageBreak)
                                {
                                    contents.Add(new FileText { Text = "\n" });
                                }
                                else if (runElement is Drawing drawing)
                                {
                                    var blip = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().FirstOrDefault();
                                    if (blip != null && blip.Embed?.Value != null)
                                    {
                                        string embed = blip.Embed.Value;
                                        var imagePart = (ImagePart)wordDoc.MainDocumentPart.GetPartById(embed);
                                        using (var stream = imagePart.GetStream())
                                        using (var memoryStreamImage = new MemoryStream())
                                        {
                                            stream.CopyTo(memoryStreamImage);
                                            byte[] imageData = memoryStreamImage.ToArray();
                                            if (imageData.Length > 10_000) // Used to filter out logos etc
                                            {
                                                contents.Add(new FileImage
                                                {
                                                    ImageData = imageData,
                                                    ImageType = imagePart.ContentType
                                                });
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    //Console.WriteLine("Unknown element: " + runElement.GetType().Name);
                                }
                            }
                        }
                        else if (paragraphElement is Hyperlink hyperlink)
                        {
                            var hyperlinkRel = wordDoc.MainDocumentPart.HyperlinkRelationships.FirstOrDefault(h => h.Id == hyperlink.Id);
                            var url = hyperlinkRel?.Uri.ToString() ?? string.Empty;
                            string displayText = hyperlink.InnerText;
                            contents.Add(new FileText { Text = $"[{displayText}]({url})" });
                        }
                        else if (paragraphElement is ParagraphProperties paragraphProperties)
                        {
                            if (paragraphProperties.NumberingProperties != null)
                            {
                               contents.Add(new FileText { Text = "* " });
                            }
                        }
                        else
                        {
                            //Console.WriteLine("Unknown element: " + paragraphElement.GetType().Name);
                        }
                    }
                    contents.Add(new FileText { Text = "\n" });
                }
            }
        }
        return contents;
    }

    public static string GetMimeTypeFromFile(ChatFile file)
    {
        if (file.FileType == FileTypeOld.Image)
        {
            string extention = file.FileName.Split('.').Last();
            if (extention == "png")
            {
                return "image/png";
            }
            else if (extention == "jpg" || extention == "jpeg")
            {
                return "image/jpeg";
            }
            else
            {
                throw new Exception("Unsupported image type: " + extention);
            }
        }
        else if (file.FileType == FileTypeOld.Text)
        {
            return "text/plain";
        }
        else if (file.FileType == FileTypeOld.Composite)
        {
            string extention = file.FileName.Split('.').Last();
            if (extention == "pdf")
            {
                return "application/pdf";
            }
            else if (extention == "docx")
            {
                return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            }
            else
            {
                throw new Exception("Unsupported composite type: " + extention);
            }
        }
        else
        {
            return "text/plain";
        }
    }

    public static FileTypeOld GetFileTypeFromName(string name)
    {
        string extention = name.Split('.').Last();
        if (ImageFiles.Contains(extention))
        {
            return FileTypeOld.Image;
        }
        else if (TextFiles.Contains(extention))
        {
            return FileTypeOld.Text;
        }
        else if (CompositeFiles.Contains(extention))
        {
            return FileTypeOld.Composite;
        }
        else
        {
            return FileTypeOld.Text;
        }
    }

    public static string GetTextFromFile(ChatFile file)
    {
        if (file.Bytes == null)
        {
            throw new Exception("File is empty");
        }
        if (file.FileType == FileTypeOld.Image)
        {
            throw new Exception("Imagefiles can not be converted to text");
        }
        else
        {
            string extension = file.FileName.Split('.').Last();
            if (extension == "pdf")
            {
                return ExtractTextFromPdf(file);
            }
            else if (extension == "docx")
            {
                return ExtractTextFromDocx(file);
            }
            else if (extension == "xlsx")
            {
                throw new Exception("XLSX files can not be converted to text");
            }
            else
            {
                try
                {
                    string text = Encoding.UTF8.GetString(file.Bytes!);
                    return text;
                }
                catch
                {
                    throw new Exception("Failed to convert file to text");
                }
            }
        }
    }


    public static OpenAI.Chat.ChatMessage GetOpenAIMessage(ChatFile file)
    {
        List<ChatMessageContentPart> parts = new List<ChatMessageContentPart>();
        parts.Add(ChatMessageContentPart.CreateTextPart("File: " + file.FileName + "\n"));

        if (file.FileType == FileTypeOld.Image)
        {
            var imagePart = ChatMessageContentPart.CreateImagePart(
                imageBytes: new BinaryData(file.Bytes!),
                imageBytesMediaType: GetMimeTypeFromFile(file)
                );
            parts.Add(imagePart);
        }
        else if (file.FileType == FileTypeOld.Text)
        {
            var textPart = ChatMessageContentPart.CreateTextPart(GetTextFromFile(file));
            parts.Add(textPart);
        }
        else
        {
            string fileExtension = file.FileName.Split('.').Last();

            if (fileExtension == "pdf")
            {
                var content = ExtractContentFromPdf(file);

                foreach (var item in content)
                {
                    if (item is FileText pdfText)
                    {
                        var textPart = ChatMessageContentPart.CreateTextPart(pdfText.Text);
                        parts.Add(textPart);
                    }
                    else if (item is FileImage pdfImage)
                    {
                        var imagePart = ChatMessageContentPart.CreateImagePart(
                            imageBytes: new BinaryData(pdfImage.ImageData),
                            imageBytesMediaType: "image/jpeg"
                            );
                        parts.Add(imagePart);
                    }
                }
            }
            else if (fileExtension == "docx")
            {

               var content = ExtractContentFromDocx(file);

                foreach (var item in content)
                {
                    if (item is FileText docxText)
                    {
                        var textPart = ChatMessageContentPart.CreateTextPart(docxText.Text);
                        parts.Add(textPart);
                    }
                    else if (item is FileImage docxImage)
                    {
                        if (docxImage.ImageType == "image/jpeg" || docxImage.ImageType == "image/png")
                        {
                            var imagePart = ChatMessageContentPart.CreateImagePart(
                                imageBytes: new BinaryData(docxImage.ImageData),
                                imageBytesMediaType: docxImage.ImageType
                                );
                            parts.Add(imagePart);
                        }
                        else
                        {
                            var textPart = ChatMessageContentPart.CreateTextPart("[Unsupported image type " + docxImage.ImageType + "]\n");
                        }

                    }
                }
            }
            else
            {
                throw new Exception("Unsupported file type: " + fileExtension);
            }
        }

        return new UserChatMessage(parts);
    }


}
*/

public class CustomTextExtractionStrategy : ITextExtractionStrategy, IEventListener
{
    private Vector lastStart = null!;

    private Vector lastEnd = null!;

    private readonly StringBuilder result = new StringBuilder();

    public virtual void EventOccurred(IEventData data, EventType type)
    {
        if (!type.Equals(EventType.RENDER_TEXT))
        {
            return;
        }

        TextRenderInfo textRenderInfo = (TextRenderInfo)data;
        bool flag = result.Length == 0;
        bool flag2 = false;
        LineSegment baseline = textRenderInfo.GetBaseline();
        Vector startPoint = baseline.GetStartPoint();
        Vector endPoint = baseline.GetEndPoint();
        if (!flag)
        {
            Vector vector = lastStart;
            Vector vector2 = lastEnd;
            float num = vector2.Subtract(vector).Cross(vector.Subtract(startPoint)).LengthSquared() / vector2.Subtract(vector).LengthSquared();
            float num2 = 1f;
            if (num > num2)
            {
                flag2 = true;
            }
        }

        if (flag2)
        {
            result.Append("\n");
        }
        else if (!flag && result[result.Length - 1] != ' ' && textRenderInfo.GetText().Length > 0 && textRenderInfo.GetText()[0] != ' ' && lastEnd.Subtract(startPoint).Length() > textRenderInfo.GetSingleSpaceWidth() / 4f) // Changed from 2f to 4f
        {
            result.Append(" ");
        }

        result.Append(textRenderInfo.GetText());
        lastStart = startPoint;
        lastEnd = endPoint;
    }

    public virtual ICollection<EventType> GetSupportedEvents()
    {
        return JavaCollectionsUtil.UnmodifiableSet(new LinkedHashSet<EventType>(JavaCollectionsUtil.SingletonList(EventType.RENDER_TEXT)));
    }

    public virtual string GetResultantText()
    {
        return result.ToString();
    }

}


/*
public abstract class FileContent { }
public class FileText : FileContent
{
    public string Text { get; set; } = null!;
}
public class FileImage : FileContent
{
    public byte[] ImageData { get; set; } = null!;
    public string ImageType { get; set; } = null!;
}
*/


public enum FileType
{
    txt,
    csv,
    json,
    html,
    pdf,
    docx,
    png,
    jpg,
    jpeg,
    bmp,
    other
}