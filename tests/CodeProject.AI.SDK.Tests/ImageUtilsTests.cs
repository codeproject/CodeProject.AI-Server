using Xunit;
using CodeProject.AI.SDK.Utils;
using SkiaSharp;

namespace CodeProject.AI.SDK.Utils.Tests
{
    public class ImageUtilsTests
    {
        const string TestImageFilename = "test.jpg";
        [Fact]
        public void GetImage_WithValidFilename_ReturnsSKImage()
        {
            // Arrange

            // Act
            SKImage? image = ImageUtils.GetImage(TestImageFilename);

            // Assert
            Assert.NotNull(image);
            // Add more assertions as needed
        }

        [Fact]
        public void GetImage_WithInvalidFilename_ReturnsNull()
        {
            // Arrange

            // Act
            SKImage? image = ImageUtils.GetImage("invalid.jpg");

            // Assert
            Assert.Null(image);
            // Add more assertions as needed
        }

        [Fact]
        public void GetImage_WithValidImageData_ReturnsSKImage()
        {
            // Arrange
            // read the image data from a file into a byte array
            byte[]? imageData = System.IO.File.ReadAllBytes(TestImageFilename);

            // Act
            SKImage? image = ImageUtils.GetImage(imageData);

            // Assert
            Assert.NotNull(image);
            // Add more assertions as needed
        }

        [Fact]
        public void GetImage_WithInvalidImageData_ReturnsNull()
        {
            // Arrange
            byte[]? imageData = new byte[] { 0, 1, 2, 3, 4, };

            // Act
            SKImage? image = ImageUtils.GetImage(imageData);

            // Assert
            Assert.Null(image);
            // Add more assertions as needed
        }

        [Fact]
        public void GetImage_WithValidImageStream_ReturnsSKImage()
        {
            // Arrange
            Stream imageStream = new FileStream(TestImageFilename, FileMode.Open);
            
            // Act
            SKImage? image = ImageUtils.GetImage(imageStream);

            // Assert
            Assert.NotNull(image);
            // Add more assertions as needed
        }

        [Fact]
        public void GetImage_WithInvalidImageStream_ReturnsNull()
        {
            // Arrange
            Stream imageStream = new MemoryStream(new byte[] {});

            // Act
            SKImage? image = ImageUtils.GetImage(imageStream);

            // Assert
            Assert.Null(image);
            // Add more assertions as needed
        }
    }
}
