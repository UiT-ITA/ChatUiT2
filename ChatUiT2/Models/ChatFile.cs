using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;
using iText.Commons.Utils;
using iText.Kernel.Geom;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Xobject;
using iText.IO.Image;
using OpenAI.Chat;

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
        else if (TextFiles.Contains(extention))
        {
            return FileType.Text;
        }
        else if (CompositeFiles.Contains(extention))
        {
            return FileType.Composite;
        }
        else
        {
            return FileType.Text;
        }
    }

    // TODO: Add support for more image formats, excel and word files
    public static List<string> ImageFiles = new() { "png", "jpg", "jpeg" };
    public static List<string> TextFiles = new() { "csv", "json", "txt", /*, "xlsx"*/};
    public static List<string> CompositeFiles = new() { "pdf",/* "docx"*/};
    public static List<string> AllFiles = ImageFiles.Concat(TextFiles).Concat(CompositeFiles).ToList();

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
        else if (file.FileType == FileType.Text)
        {
            // Verify document
            
        }
        else if (file.FileType == FileType.Composite)
        {
            // Verify composite
            if (file.FileName.Split('.').Last() == "pdf")
            {
                try
                {
                    _ = ExtractContentFromPdf(file);
                }
                catch
                {
                    Console.WriteLine("Failed to extract content from pdf");
                    return false;
                }
            }
        }

        return true;
    }

    public static string GetText(ChatFile file)
    {
        if (file.Bytes == null)
        {
            throw new Exception("File is empty");
        }
        if (file.FileType == FileType.Image)
        {
            throw new Exception("Imagefiles can not be converted to text");
        }
        else
        {
            string extension = file.FileName.Split('.').Last();
            if (extension == "pdf")
            {
                return GetPdfText(file);
            }
            else if (extension == "docx")
            {
                throw new Exception("DOCX files can not be converted to text");
            }
            else if (extension == "xlsx")
            {
                throw new Exception("XLSX files can not be converted to text");
            }
            else
            {
                try
                {
                    string text = System.Text.Encoding.UTF8.GetString(file.Bytes!);
                    return text;
                }
                catch
                {
                    throw new Exception("Failed to convert file to text");
                }
            }
        }
    }

    public static string GetPdfText(ChatFile file)
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


    public static List<PdfContent> ExtractContentFromPdf(ChatFile file)
    {
        
        List<PdfContent> contents = new List<PdfContent>();
        using (PdfReader pdfReader = new PdfReader(new MemoryStream(file.Bytes!)))
        using (PdfDocument pdfDocument = new PdfDocument(pdfReader))
        {
            for (int page = 1; page <= pdfDocument.GetNumberOfPages(); page++)
            {
                var strategy = new CustomTextExtractionStrategy();
                string text = PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(page), strategy);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    contents.Add(new PdfText { Text = text });
                }
                PdfResources resources = pdfDocument.GetPage(page).GetResources();
                foreach (PdfName key in resources.GetResourceNames(PdfName.XObject))
                {
                    //PdfObject obj = resources.GetResource(PdfName.XObject, key);
                    PdfObject obj = resources.GetResourceObject(PdfName.XObject, key);
                    if (obj is PdfStream stream)
                    {
                        PdfImageXObject image = new PdfImageXObject(stream);
                        byte[] imageData = image.GetImageBytes(true);
                        if (imageData.Length > 10_000)
                            contents.Add(new PdfImage { ImageData = imageData });
                    }
                }
            }
        }
        return contents;
    }


    public static OpenAI.Chat.ChatMessage GetOpenAIMessage(ChatFile file)
    {
        List<ChatMessageContentPart> parts = new List<ChatMessageContentPart>();
        parts.Add(ChatMessageContentPart.CreateTextMessageContentPart("File: " + file.FileName + "\n"));

        if (file.FileType == FileType.Image)
        {
            var imagePart = ChatMessageContentPart.CreateImageMessageContentPart(
                imageBytes: new BinaryData(file.Bytes!),
                imageBytesMediaType: GetMimeType(file)
                );
            parts.Add(imagePart);
        }
        else if (file.FileType == FileType.Text)
        {
            var textPart = ChatMessageContentPart.CreateTextMessageContentPart(GetText(file));
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
                    if (item is PdfText pdfText)
                    {
                        var textPart = ChatMessageContentPart.CreateTextMessageContentPart(pdfText.Text);
                        parts.Add(textPart);
                    }
                    else if (item is PdfImage pdfImage)
                    {
                        var imagePart = ChatMessageContentPart.CreateImageMessageContentPart(
                            imageBytes: new BinaryData(pdfImage.ImageData),
                            imageBytesMediaType: "image/jpeg"
                            );
                        parts.Add(imagePart);
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

public enum FileType
{
    Image,
    Text,
    Composite
}




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

public abstract class PdfContent { }
public class PdfText : PdfContent
{
    public string Text { get; set; } = null!;
}
public class PdfImage : PdfContent
{
    public byte[] ImageData { get; set; } = null!;
}
