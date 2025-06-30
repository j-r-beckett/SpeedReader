using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Test.Visualization;

public class BasicVizBuilderTests
{
    [Fact]
    public void Build_WithNoMergedResults_ReturnsCloneOfSourceImage()
    {
        // Arrange
        var sourceImage = new Image<Rgb24>(100, 100, Color.White);
        var vizBuilder = new BasicVizBuilder(sourceImage);

        // Act
        var result = vizBuilder.Render();

        // Assert
        Assert.NotSame(sourceImage, result);
        Assert.Equal(sourceImage.Width, result.Width);
        Assert.Equal(sourceImage.Height, result.Height);
    }

    [Fact]
    public void Build_WithMergedResults_ReturnsAnnotatedImage()
    {
        // Arrange
        var sourceImage = new Image<Rgb24>(200, 200, Color.White);
        var vizBuilder = new BasicVizBuilder(sourceImage);
        
        var rectangles = new List<Rectangle> { new Rectangle(10, 10, 50, 20) };
        var texts = new List<string> { "Hello" };
        
        vizBuilder.AddMergedResults(rectangles, texts);

        // Act
        var result = vizBuilder.Render();

        // Assert
        Assert.NotSame(sourceImage, result);
        // The result should be different from the source since we drew on it
        // Note: We can't easily test the exact visual output without more complex image comparison
    }

    [Fact]
    public void AddDetectionResults_DoesNotThrow()
    {
        // Arrange
        var sourceImage = new Image<Rgb24>(100, 100, Color.White);
        var vizBuilder = new BasicVizBuilder(sourceImage);
        var rectangles = new List<Rectangle> { new Rectangle(0, 0, 10, 10) };

        // Act & Assert - should not throw
        vizBuilder.AddDetectionResults(rectangles, default);
    }

    [Fact]
    public void AddRecognitionResults_DoesNotThrow()
    {
        // Arrange
        var sourceImage = new Image<Rgb24>(100, 100, Color.White);
        var vizBuilder = new BasicVizBuilder(sourceImage);
        var rectangles = new List<Rectangle> { new Rectangle(0, 0, 10, 10) };
        var texts = new List<string> { "test" };

        // Act & Assert - should not throw
        vizBuilder.AddRecognitionResults(rectangles, texts);
    }
}