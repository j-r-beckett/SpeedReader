using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit.Abstractions;

namespace Ocr.Test;

public class ResamplingTests
{
    private readonly ITestOutputHelper _output;

    public ResamplingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(100, 50)]    // Landscape
    [InlineData(50, 100)]    // Portrait
    [InlineData(1920, 1080)] // Large landscape
    [InlineData(480, 640)]   // Large portrait
    [InlineData(32, 32)]     // Already divisible by 32
    [InlineData(1333, 736)]  // Exact target size
    [InlineData(1, 1)]
    public void CalculateDimensions_MatchesActualProcessing(int width, int height)
    {
        // Arrange
        using var testImage = new Image<Rgb24>(width, height, new Rgb24(128, 128, 128));

        // Act
        var calculatedDimensions = CalculateDimensions(testImage);
        var actualDimensions = ExpectedDimensions(testImage);

        // Log dimensions
        _output.WriteLine($"Start dimensions: {width}x{height}");
        _output.WriteLine($"End dimensions: {calculatedDimensions.width}x{calculatedDimensions.height}");

        // Assert
        Assert.Equal(actualDimensions.width, calculatedDimensions.width);
        Assert.Equal(actualDimensions.height, calculatedDimensions.height);
    }

    private static (int width, int height) CalculateDimensions(Image image)
    {
        int width = image.Width;
        int height = image.Height;
        double scale = Math.Min((double)1333 / width, (double)736 / height);
        int fittedWidth = (int)Math.Round(width * scale);
        int fittedHeight = (int)Math.Round(height * scale);
        int paddedWidth = (fittedWidth + 31) / 32 * 32;
        int paddedHeight = (fittedHeight + 31) / 32 * 32;
        return (paddedWidth, paddedHeight);
    }

    private static (int width, int height) ExpectedDimensions(Image<Rgb24> sampleImage)
    {
        using var cloned = sampleImage.Clone();

        // Step 1: Resize with aspect ratio preservation
        cloned.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(1333, 736),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Bicubic
        }));

        // Step 2: Actually pad to make dimensions divisible by 32
        int paddedWidth = (cloned.Width + 31) / 32 * 32;
        int paddedHeight = (cloned.Height + 31) / 32 * 32;
        cloned.Mutate(x => x.Pad(paddedWidth, paddedHeight, Color.Black));

        return (cloned.Width, cloned.Height);
    }
}
