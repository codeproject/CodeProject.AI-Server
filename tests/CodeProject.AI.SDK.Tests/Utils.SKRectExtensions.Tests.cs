using SkiaSharp;

using Xunit;

namespace CodeProject.AI.SDK.Utils.Tests
{
    public class SKRectExtensionsTests
    {
        [Fact]
        public void Area_ReturnsCorrectArea()
        {
            // Arrange
            SKRect rect = new SKRect(0, 0, 5, 10);

            // Act
            float area = rect.Area();

            // Assert
            Assert.Equal(50, area);
        }
    }
}
