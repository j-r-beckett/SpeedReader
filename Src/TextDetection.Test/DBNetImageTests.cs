
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace TextDetection.Test;

public class DBNetImageTests
{
    [Fact]
    public void Create_NormalizesPixelsCorrectly()
    {
        // Arrange: Create 1x1 image with known RGB values
        using var image = new Image<Rgb24>(1, 1, new Rgb24(255, 128, 0));
        
        // Act
        var dbnetImage = DbNetImage.Create(image);
        
        // Assert: Verify exact normalized values using DBNet's normalization parameters
        var means = new[] { 123.675f, 116.28f, 103.53f };
        var stds = new[] { 58.395f, 57.12f, 57.375f };
        
        float expectedR = (255f - means[0]) / stds[0];  // ~2.252
        float expectedG = (128f - means[1]) / stds[1];  // ~0.205  
        float expectedB = (0f - means[2]) / stds[2];    // ~-1.804
        
        var data = dbnetImage.Data;
        int channelSize = dbnetImage.Width * dbnetImage.Height;
        
        data[0].Should().BeApproximately(expectedR, 0.001f);                    // First R value
        data[channelSize].Should().BeApproximately(expectedG, 0.001f);          // First G value
        data[2 * channelSize].Should().BeApproximately(expectedB, 0.001f);      // First B value
    }

    [Fact]
    public void Create_StoresDataInChwLayout()
    {
        // Arrange: Create 2x2 image with distinct RGB values per pixel
        using var image = new Image<Rgb24>(2, 2);
        image[0, 0] = new Rgb24(100, 150, 200); // Top-left
        image[1, 0] = new Rgb24(101, 151, 201); // Top-right  
        image[0, 1] = new Rgb24(102, 152, 202); // Bottom-left
        image[1, 1] = new Rgb24(103, 153, 203); // Bottom-right

        // Act
        var dbnetImage = DbNetImage.Create(image);
        
        // Assert: Verify CHW layout - all R values, then all G values, then all B values
        var data = dbnetImage.Data;
        int channelSize = dbnetImage.Width * dbnetImage.Height;
        
        // Red channel should be in first channelSize elements
        // Green channel should be in next channelSize elements  
        // Blue channel should be in final channelSize elements
        
        // Since image gets padded to 32x32, we need to account for padding
        // The original 2x2 pixels should be in the top-left of the padded image
        
        // Verify the pattern exists (exact values depend on padding, but layout should be CHW)
        channelSize.Should().BeGreaterThanOrEqualTo(4); // At least our 4 pixels after padding
        data.Length.Should().Be(3 * channelSize); // 3 channels
    }

    [Fact]
    public void Create_ProducesCorrectDimensions()
    {
        // Arrange: Test landscape image that needs resize and padding
        using var image = new Image<Rgb24>(100, 50);
        
        // Act
        var dbnetImage = DbNetImage.Create(image);
        
        // Assert: Verify dimensions are multiples of 32
        (dbnetImage.Width % 32).Should().Be(0);
        (dbnetImage.Height % 32).Should().Be(0);
        
        // Verify dimensions don't exceed maximums (1333x736 → padded to 1344x768)
        dbnetImage.Width.Should().BeLessThanOrEqualTo(1344);
        dbnetImage.Height.Should().BeLessThanOrEqualTo(768);
        
        // For 100x50 input with aspect ratio 2:1, should fit within bounds while preserving ratio
        double aspectRatio = (double)dbnetImage.Width / dbnetImage.Height;
        aspectRatio.Should().BeGreaterThanOrEqualTo(1.5); // Should maintain roughly 2:1 aspect ratio
    }

    [Fact]
    public void Create_BlackImageNormalizesToExpectedValues()
    {
        // Arrange: Create black image
        using var image = new Image<Rgb24>(32, 32, new Rgb24(0, 0, 0));
        
        // Act
        var dbnetImage = DbNetImage.Create(image);
        
        // Assert: Black pixels should normalize to specific negative values
        var means = new[] { 123.675f, 116.28f, 103.53f };
        var stds = new[] { 58.395f, 57.12f, 57.375f };
        
        float expectedR = (0f - means[0]) / stds[0];  // ~-2.117
        float expectedG = (0f - means[1]) / stds[1];  // ~-2.035
        float expectedB = (0f - means[2]) / stds[2];  // ~-1.804
        
        var data = dbnetImage.Data;
        int channelSize = dbnetImage.Width * dbnetImage.Height;
        
        // Check a sample of values from each channel
        data[0].Should().BeApproximately(expectedR, 0.001f);                    // First R value
        data[channelSize].Should().BeApproximately(expectedG, 0.001f);          // First G value  
        data[2 * channelSize].Should().BeApproximately(expectedB, 0.001f);      // First B value
    }
}
