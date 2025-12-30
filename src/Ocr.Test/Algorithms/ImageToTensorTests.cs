// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SpeedReader.Ocr.Algorithms;

namespace SpeedReader.Ocr.Test.Algorithms;

public class ImageToTensorTests
{
    [Fact]
    public void ToNormalizedChwTensor_1x1Image_NormalizesPixelCorrectly()
    {
        // Arrange
        using var image = new Image<Rgb24>(1, 1);
        image[0, 0] = new Rgb24(100, 150, 200);
        var rect = new Rectangle(0, 0, 1, 1);
        ReadOnlySpan<float> means = [50.0f, 100.0f, 150.0f];
        ReadOnlySpan<float> stds = [10.0f, 25.0f, 50.0f];

        // Act
        var result = image.ToNormalizedChwTensor(rect, means, stds);

        // Assert
        Assert.Equal(3, result.Length);
        Assert.Equal((100 - 50.0f) / 10.0f, result[0], precision: 5);  // R channel
        Assert.Equal((150 - 100.0f) / 25.0f, result[1], precision: 5); // G channel
        Assert.Equal((200 - 150.0f) / 50.0f, result[2], precision: 5); // B channel
    }

    [Fact]
    public void ToNormalizedChwTensor_2x2Image_NormalizesAndLayoutsCorrectly()
    {
        // Arrange
        using var image = new Image<Rgb24>(2, 2);
        image[0, 0] = new Rgb24(10, 20, 30);  // Top-left
        image[1, 0] = new Rgb24(40, 50, 60);  // Top-right
        image[0, 1] = new Rgb24(70, 80, 90);  // Bottom-left
        image[1, 1] = new Rgb24(100, 110, 120); // Bottom-right
        var rect = new Rectangle(0, 0, 2, 2);
        ReadOnlySpan<float> means = [0.0f, 0.0f, 0.0f];
        ReadOnlySpan<float> stds = [1.0f, 1.0f, 1.0f];

        // Act
        var result = image.ToNormalizedChwTensor(rect, means, stds);

        // Assert
        Assert.Equal(12, result.Length); // 3 channels × 2 height × 2 width

        // R channel (indices 0-3): [10, 40, 70, 100]
        Assert.Equal(10.0f, result[0], precision: 5);  // [0,0]
        Assert.Equal(40.0f, result[1], precision: 5);  // [1,0]
        Assert.Equal(70.0f, result[2], precision: 5);  // [0,1]
        Assert.Equal(100.0f, result[3], precision: 5); // [1,1]

        // G channel (indices 4-7): [20, 50, 80, 110]
        Assert.Equal(20.0f, result[4], precision: 5);  // [0,0]
        Assert.Equal(50.0f, result[5], precision: 5);  // [1,0]
        Assert.Equal(80.0f, result[6], precision: 5);  // [0,1]
        Assert.Equal(110.0f, result[7], precision: 5); // [1,1]

        // B channel (indices 8-11): [30, 60, 90, 120]
        Assert.Equal(30.0f, result[8], precision: 5);   // [0,0]
        Assert.Equal(60.0f, result[9], precision: 5);   // [1,0]
        Assert.Equal(90.0f, result[10], precision: 5);  // [0,1]
        Assert.Equal(120.0f, result[11], precision: 5); // [1,1]
    }

    [Fact]
    public void ToNormalizedChwTensor_UniformColorImage_ProducesChannelBlocks()
    {
        // Arrange
        using var image = new Image<Rgb24>(3, 3);
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                image[x, y] = new Rgb24(250, 150, 50);
            }
        }
        var rect = new Rectangle(0, 0, 3, 3);
        ReadOnlySpan<float> means = [0.0f, 0.0f, 0.0f];
        ReadOnlySpan<float> stds = [1.0f, 1.0f, 1.0f];

        // Act
        var result = image.ToNormalizedChwTensor(rect, means, stds);

        // Assert
        Assert.Equal(27, result.Length); // 3 channels × 3 height × 3 width

        // R block (indices 0-8): all 250
        for (int i = 0; i < 9; i++)
        {
            Assert.Equal(250.0f, result[i], precision: 5);
        }

        // G block (indices 9-17): all 150
        for (int i = 9; i < 18; i++)
        {
            Assert.Equal(150.0f, result[i], precision: 5);
        }

        // B block (indices 18-26): all 50
        for (int i = 18; i < 27; i++)
        {
            Assert.Equal(50.0f, result[i], precision: 5);
        }
    }

    [Theory]
    [InlineData(0, 0)]      // Top-left corner
    [InlineData(5, 0)]      // Top-right area
    [InlineData(0, 5)]      // Bottom-left area
    [InlineData(3, 3)]      // Center
    [InlineData(5, 5)]      // Bottom-right corner
    public void ToNormalizedChwTensor_RedRectangleWithBorder_ExtractsCorrectRegion(int rectX, int rectY)
    {
        // Arrange
        using var image = new Image<Rgb24>(10, 10);

        // Fill with white background
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                image[x, y] = new Rgb24(255, 255, 255);
            }
        }

        // Draw red 2x2 rectangle at position determined by test parameters
        int redX = rectX + 1;
        int redY = rectY + 1;
        for (int y = redY; y < redY + 2; y++)
        {
            for (int x = redX; x < redX + 2; x++)
            {
                image[x, y] = new Rgb24(255, 0, 0);
            }
        }

        // Extract 4x4 rectangle that includes 1-pixel white border around red 2x2
        var rect = new Rectangle(rectX, rectY, 4, 4);
        ReadOnlySpan<float> means = [0.0f, 0.0f, 0.0f];
        ReadOnlySpan<float> stds = [1.0f, 1.0f, 1.0f];

        // Act
        var result = image.ToNormalizedChwTensor(rect, means, stds);

        // Assert
        Assert.Equal(48, result.Length); // 3 channels × 4 height × 4 width

        // Helper to get tensor value at position
        float GetR(int x, int y) => result[0 * 4 * 4 + y * 4 + x];
        float GetG(int x, int y) => result[1 * 4 * 4 + y * 4 + x];
        float GetB(int x, int y) => result[2 * 4 * 4 + y * 4 + x];

        // Check corner (should be white: R=255, G=255, B=255)
        Assert.Equal(255.0f, GetR(0, 0), precision: 5);
        Assert.Equal(255.0f, GetG(0, 0), precision: 5);
        Assert.Equal(255.0f, GetB(0, 0), precision: 5);

        // Check red area center (position (1,1) and (2,2) should be red)
        Assert.Equal(255.0f, GetR(1, 1), precision: 5);
        Assert.Equal(0.0f, GetG(1, 1), precision: 5);
        Assert.Equal(0.0f, GetB(1, 1), precision: 5);

        Assert.Equal(255.0f, GetR(2, 2), precision: 5);
        Assert.Equal(0.0f, GetG(2, 2), precision: 5);
        Assert.Equal(0.0f, GetB(2, 2), precision: 5);

        // Check bottom-right corner (should be white)
        Assert.Equal(255.0f, GetR(3, 3), precision: 5);
        Assert.Equal(255.0f, GetG(3, 3), precision: 5);
        Assert.Equal(255.0f, GetB(3, 3), precision: 5);
    }

    [Fact]
    public void ToNormalizedChwTensor_InvalidMeansLength_ThrowsArgumentException()
    {
        // Arrange
        using var image = new Image<Rgb24>(1, 1);
        var rect = new Rectangle(0, 0, 1, 1);
        float[] means = [1.0f, 2.0f]; // Invalid: length 2
        float[] stds = [1.0f, 1.0f, 1.0f];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => image.ToNormalizedChwTensor(rect, means, stds));
    }

    [Fact]
    public void ToNormalizedChwTensor_InvalidStdsLength_ThrowsArgumentException()
    {
        // Arrange
        using var image = new Image<Rgb24>(1, 1);
        var rect = new Rectangle(0, 0, 1, 1);
        float[] means = [1.0f, 1.0f, 1.0f];
        float[] stds = [1.0f, 2.0f, 3.0f, 4.0f]; // Invalid: length 4

        // Act & Assert
        Assert.Throws<ArgumentException>(() => image.ToNormalizedChwTensor(rect, means, stds));
    }
}
