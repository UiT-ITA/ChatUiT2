using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Xobject;
using iText.Kernel.Pdf;
using iText.Commons.Utils;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Text;
using iText.Kernel.Geom;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ChatUiT2.Models;
using OpenAI.Chat;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;

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
        return new List<ChatFilePart> { new ImageFilePart(data) };
    }

    private static List<ChatFilePart> HandleDocxFile(byte[] data)
    {
        return ExtractContentFromDocx(data);
    }

    private static List<ChatFilePart> HandlePdfFile(byte[] data)
    {
        return ExtractContentFromPdf(data);
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


    public static byte[] ResizeImage(byte[] imageBytes, int width, int height, bool innerDimensions = true)
    {
        using (var image = Image.Load(imageBytes))
        {
            var resizeOptions = new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = innerDimensions ? ResizeMode.Crop : ResizeMode.Max
            };
            image.Mutate(x => x.Resize(resizeOptions));
            using (var outputStream = new MemoryStream())
            {
                image.Save(outputStream, new PngEncoder());
                return outputStream.ToArray();
            }
        }
    }

    public static string ImageToBase64(byte[] imageBytes, bool resize = true)
    {
        byte[] imageToEncode = imageBytes;
        if (resize)
        {
            var (width, height) = GetImageDimensions(imageBytes);
            if (width > 1024 || height > 1024)
            {
                imageToEncode = ResizeImage(imageBytes, 1024, 1024, innerDimensions: false);
            }
        }
        using (var image = Image.Load(imageToEncode))
        using (var outputStream = new MemoryStream())
        {
            image.Save(outputStream, new PngEncoder());
            var base64 = Convert.ToBase64String(outputStream.ToArray());
            return $"data:image/png;base64,{base64}";
        }
    }

    public static (int, int) GetImageDimensions(byte[] imageBytes)
    {
        using (var image = Image.Load(imageBytes))
        {
            return (image.Width, image.Height);
        }
    }

    public static OpenAI.Chat.ChatMessage GetOpenAIMessage(this ChatFile file, bool userMessage = true)
    {
        List<ChatMessageContentPart> parts = new List<ChatMessageContentPart>();
        parts.Add(ChatMessageContentPart.CreateTextPart("File: " + file.FileName + "\n"));

        foreach (var part in file.Parts)
        {
            if (part is TextFilePart textPart)
            {
                var messagePart = ChatMessageContentPart.CreateTextPart(textPart.Data);
                parts.Add(messagePart);
            }
            else if (part is ImageFilePart imagePart)
            {
                var messagePart = ChatMessageContentPart.CreateImagePart(imageBytes: new BinaryData(imagePart.Data), imageBytesMediaType: "image/png");
                parts.Add(messagePart);
            }
        }
        if (userMessage)
        {
            return new UserChatMessage(parts);
        }
        else
        {
            return new AssistantChatMessage(parts);
        }
    }
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