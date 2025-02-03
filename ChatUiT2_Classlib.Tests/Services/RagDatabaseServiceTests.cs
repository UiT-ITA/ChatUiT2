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
}
