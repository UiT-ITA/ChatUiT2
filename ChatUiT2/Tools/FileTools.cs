using iText.Kernel.Pdf.Canvas.Parser;
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

namespace ChatUiT2.Tools;

public class FileTools
{
    // TODO: Add support for more image formats, excel and word files
    public static List<string> ImageFiles = new() { "png", "jpg", "jpeg" };
    public static List<string> TextFiles = new() { "csv", "json", "txt", /*, "xlsx"*/};
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
                //Console.WriteLine("The image is corrupted.");
                return false;
            }
        }
        else if (file.FileType == FileType.Text)
        {
            // Verify document

        }
        else if (file.FileType == FileType.Composite)
        {
            string extention = file.FileName.Split('.').Last();
            // Verify composite
            if (extention == "pdf")
            {
                try
                {
                    _ = ExtractContentFromPdf(file);
                }
                catch (Exception)
                {
                    //Console.WriteLine("Failed to extract content from pdf");
                    //Console.WriteLine(ex.Message);
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
        using (PdfReader pdfReader = new PdfReader(new MemoryStream(file.Bytes)))
        using (PdfDocument pdfDocument = new PdfDocument(pdfReader))
        {
            for (int page = 1; page <= pdfDocument.GetNumberOfPages(); page++)
            {
                var strategy = new CustomTextExtractionStrategy();
                string text = PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(page), strategy);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    contents.Add(new FileText { Text = text });
                }
                PdfResources resources = pdfDocument.GetPage(page).GetResources();
                foreach (PdfName key in resources.GetResourceNames(PdfName.XObject))
                {
                    PdfObject obj = resources.GetResourceObject(PdfName.XObject, key);
                    if (obj is PdfStream stream)
                    {
                        PdfImageXObject image = new PdfImageXObject(stream);
                        byte[] imageData = image.GetImageBytes(true);
                        if (imageData.Length > 10_000)
                        {
                            contents.Add(new FileImage { ImageData = imageData });
                        }
                    }
                }
            }
        }
        return contents;
    }
    
    public static List<FileContent> ExtractContentFromDocxOld(ChatFile file)
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
                    StringBuilder paragraphText = new StringBuilder();
                    foreach (var run in paragraph.Elements<Run>())
                    {
                        foreach (var runElement in run.Elements())
                        {
                            if (runElement is Text textElement)
                            {
                                paragraphText.Append(textElement.Text);
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
                                        if (imageData.Length > 10_000)
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
                        }
                    }
                    if (paragraphText.Length > 0)
                    {
                        contents.Add(new FileText { Text = paragraphText.ToString() });
                    }
                }
                else
                {
                    //Console.WriteLine("Unknown element: " + element.GetType().Name);
                }
            }
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
        if (file.FileType == FileType.Image)
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
        else if (file.FileType == FileType.Text)
        {
            return "text/plain";
        }
        else if (file.FileType == FileType.Composite)
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

    public static string GetTextFromFile(ChatFile file)
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
        parts.Add(ChatMessageContentPart.CreateTextMessageContentPart("File: " + file.FileName + "\n"));

        if (file.FileType == FileType.Image)
        {
            var imagePart = ChatMessageContentPart.CreateImageMessageContentPart(
                imageBytes: new BinaryData(file.Bytes!),
                imageBytesMediaType: GetMimeTypeFromFile(file)
                );
            parts.Add(imagePart);
        }
        else if (file.FileType == FileType.Text)
        {
            var textPart = ChatMessageContentPart.CreateTextMessageContentPart(GetTextFromFile(file));
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
                        var textPart = ChatMessageContentPart.CreateTextMessageContentPart(pdfText.Text);
                        parts.Add(textPart);
                    }
                    else if (item is FileImage pdfImage)
                    {
                        var imagePart = ChatMessageContentPart.CreateImageMessageContentPart(
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
                        var textPart = ChatMessageContentPart.CreateTextMessageContentPart(docxText.Text);
                        parts.Add(textPart);
                    }
                    else if (item is FileImage docxImage)
                    {
                        if (docxImage.ImageType == "image/jpeg" || docxImage.ImageType == "image/png")
                        {
                            var imagePart = ChatMessageContentPart.CreateImageMessageContentPart(
                                imageBytes: new BinaryData(docxImage.ImageData),
                                imageBytesMediaType: docxImage.ImageType
                                );
                            parts.Add(imagePart);
                        }
                        else
                        {
                            var textPart = ChatMessageContentPart.CreateTextMessageContentPart("[Unsupported image type " + docxImage.ImageType + "]\n");
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