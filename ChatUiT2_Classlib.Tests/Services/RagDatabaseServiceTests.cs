using ChatUiT2.Services;

namespace ChatUiT2_Classlib.Tests.Services;
public class RagDatabaseServiceTests
{
    [Fact]
    public void ReplaceHtmlLinebreaksWithNewline_SingleBrTag_ShouldReplaceWithNewline()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "Hello<br>World";
        string expected = "Hello\nWorld";

        // Act
        string result = service.ReplaceHtmlLinebreaksWithNewline(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplaceHtmlLinebreaksWithNewline_MultipleBrTags_ShouldReplaceWithNewlines()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "Hello<br>World<br>!";
        string expected = "Hello\nWorld\n!";

        // Act
        string result = service.ReplaceHtmlLinebreaksWithNewline(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplaceHtmlLinebreaksWithNewline_BrTagsWithEndTag_ShouldReplaceWithNewline()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "Hello<br />World<br/>!";
        string expected = "Hello\nWorld\n!";

        // Act
        string result = service.ReplaceHtmlLinebreaksWithNewline(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplaceHtmlLinebreaksWithNewline_VariousCasing_ShouldReplaceWithNewline()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "Hello<BR>World<Br/>";
        string expected = "Hello\nWorld\n";

        // Act
        string result = service.ReplaceHtmlLinebreaksWithNewline(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplaceHtmlLinebreaksWithNewline_EmptyString_ShouldReturnEmptyString()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "";
        string expected = "";

        // Act
        string result = service.ReplaceHtmlLinebreaksWithNewline(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplaceHtmlLinebreaksWithNewline_NoBrTags_ShouldReturnInputString()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "Hello World!";
        string expected = "Hello World!";

        // Act
        string result = service.ReplaceHtmlLinebreaksWithNewline(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RemoveAllHtmlTagsFromString_VariousTags_ShouldRemoveAllTags()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "<p>Hello <strong>World</strong></p>";
        string expected = "Hello World";

        // Act
        string result = service.RemoveAllHtmlTagsFromString(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RemoveAllHtmlTagsFromString_EmptyString_ShouldReturnEmptyString()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "";
        string expected = "";

        // Act
        string result = service.RemoveAllHtmlTagsFromString(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RemoveAllHtmlTagsFromString_StringWithoutTags_ShouldReturnInputString()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "Hello World";
        string expected = "Hello World";

        // Act
        string result = service.RemoveAllHtmlTagsFromString(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RemoveAllHtmlTagsFromString_NestedTags_ShouldRemoveTags()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "<div><p>Hello <strong>World</strong></p></div>";
        string expected = "Hello World";

        // Act
        string result = service.RemoveAllHtmlTagsFromString(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RemoveAllHtmlTagsFromString_SelfClosingTags_ShouldRemoveTags()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "Hello<br/>World<hr/>";
        string expected = "HelloWorld";

        // Act
        string result = service.RemoveAllHtmlTagsFromString(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RemoveAllHtmlTagsFromString_TagsWithAttributes_ShouldRemoveTagsWithTheirAttributes()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "<p class='text'>Hello <a href='https://example.com'>World</a></p>";
        string expected = "Hello World";

        // Act
        string result = service.RemoveAllHtmlTagsFromString(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SplitTextIntoParagraphs_StringWithMultipleDoubleNewlines_ShouldSplitAtDoubleNewline()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "Paragraph 1\n\nParagraph 2\n\nParagraph 3";
        var expected = new List<string> { "Paragraph 1", "Paragraph 2", "Paragraph 3" };

        // Act
        var result = service.SplitTextIntoParagraphs(input).ToList();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SplitTextIntoParagraphs_SingleParagraph_ShouldReturnSingleChunk()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "Single paragraph";
        var expected = new List<string> { "Single paragraph" };

        // Act
        var result = service.SplitTextIntoParagraphs(input).ToList();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SplitTextIntoParagraphs_EmptyString_ShouldReturnEmptyListOfChunks()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "";
        var expected = new List<string>();

        // Act
        var result = service.SplitTextIntoParagraphs(input).ToList();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SplitTextIntoParagraphs_MultipleNewlines_ShouldSplitCorrectlyAndHaveNoEmptyChunks()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "Paragraph 1\n\n\n\nParagraph 2";
        var expected = new List<string> { "Paragraph 1", "Paragraph 2" };

        // Act
        var result = service.SplitTextIntoParagraphs(input).ToList();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SplitTextIntoParagraphs_LeadingAndTrailingNewlines_ShouldSplitCorrectWithoutEmptyChunks()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "\n\nParagraph 1\n\nParagraph 2\n\n";
        var expected = new List<string> { "Paragraph 1", "Paragraph 2" };

        // Act
        var result = service.SplitTextIntoParagraphs(input).ToList();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SplitTextIntoParagraphs_WhitespaceBetweenNewlines_ShouldSplitCorrectly()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "Paragraph 1\n \nParagraph 2";
        var expected = new List<string> { "Paragraph 1", "Paragraph 2" };

        // Act
        var result = service.SplitTextIntoParagraphs(input).ToList();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SplitTextIntoParagraphs_DoNotCleanHtmlTagsSet_ShouldNotRemoveHtmlTags()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "Paragraph 1<div>123</div>\n\nParagraph 2";
        var expected = new List<string> { "Paragraph 1<div>123</div>", "Paragraph 2" };

        // Act
        var result = service.SplitTextIntoParagraphs(input, false).ToList();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SplitTextIntoParagraphs_StringWithBrTags_ShouldReplaceWithNewlineAndSplitCorrectly()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "Paragraph 1<br/><br/>Paragraph 2";
        var expected = new List<string> { "Paragraph 1", "Paragraph 2" };

        // Act
        var result = service.SplitTextIntoParagraphs(input).ToList();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SplitTextIntoParagraphs_DoNotReplaceBrTagsSet_ShouldNotReplaceWithNewline()
    {
        // Arrange
        var service = new RagDatabaseService(null, null, null, null);
        string input = "Paragraph 1<br/><br/>Paragraph 2";
        var expected = new List<string> { "Paragraph 1Paragraph 2" };

        // Act
        var result = service.SplitTextIntoParagraphs(input, true, false).ToList();

        // Assert
        Assert.Equal(expected, result);
    }
}
