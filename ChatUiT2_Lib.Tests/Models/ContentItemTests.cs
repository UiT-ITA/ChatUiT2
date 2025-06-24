using ChatUiT2.Models.RagProject;
using Xunit;

namespace ChatUiT2_Lib.Tests.Models
{
    public class ContentItemTests
    {
        [Theory]
        [InlineData("Title1", "Description1", "Content1", "Title1_Description1_Content1")]
        [InlineData("Title2", "Description2", "Content2", "Title2_Description2_Content2")]
        [InlineData("", "DescriptionOnly", "", "_DescriptionOnly_")]
        [InlineData("TitleOnly", "", "", "TitleOnly__")]
        [InlineData("", "", "", "__")]
        public void StringForContentHash_ValidProperties_ReturnsExpectedString(string title, string description, string contentText, string expectedHashString)
        {
            // Arrange
            var contentItem = new ContentItem
            {
                Title = title,
                Description = description,
                ContentText = contentText
            };

            // Act
            string actualHashString = contentItem.StringForContentHash;

            // Assert
            Assert.Equal(expectedHashString, actualHashString);
        }
    }
}