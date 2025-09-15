// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Logging;
using Ocr.Algorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TestUtils;
using Xunit.Abstractions;

namespace Ocr.Test.Algorithms;

public class OrientedRectangleCroppingTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly ILogger<OrientedRectangleCroppingTests> _logger;
    private readonly FileSystemUrlPublisher<OrientedRectangleCroppingTests> _publisher;

    public OrientedRectangleCroppingTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _logger = new TestLogger<OrientedRectangleCroppingTests>(outputHelper);
        var outputDirectory = "/tmp/oriented-rectangle-tests";
        _publisher = new FileSystemUrlPublisher<OrientedRectangleCroppingTests>(outputDirectory, _logger);
    }

    [Fact]
    public async Task AxisAlignedOrientedRectangle_GeneratesCorrectGradients()
    {
        // Arrange - Create an axis-aligned rectangle
        var imageWidth = 400;
        var imageHeight = 300;
        var p = new PointF(100, 80);  // Top-left corner
        var q = new PointF(300, 80);  // Top-right corner (horizontal edge)
        var width = 120f; // Height of rectangle

        // Generate test image with gradient-filled oriented rectangle
        using var sourceImage = OrientedRectangleTestUtils.CreateGradientOrientedRectangle(
            imageWidth, imageHeight, p, q, width);
        await _publisher.PublishAsync(sourceImage, "Source image with axis-aligned oriented rectangle");

        // Calculate oriented rectangle vertices for cropping
        var vertices = OrientedRectangleTestUtils.CalculateOrientedRectangleVertices(p, q, width);
        var orientedRect = vertices.Select(v => ((double)v.X, (double)v.Y)).ToList();

        // Test all possible vertex orderings to ensure order independence
        int permutationCount = 0;
        foreach (var permutedVertices in Permute(orientedRect))
        {
            permutationCount++;
            var permutedList = permutedVertices.ToList();

            _outputHelper.WriteLine($"Permutation {permutationCount}: [{string.Join(", ", permutedList.Select(v => $"({v.Item1:F1},{v.Item2:F1})"))}]");

            // Test the orientation detection directly
            var detectedCorners = ImageCropping.DetectOrientationAndOrderCorners(permutedList);
            _outputHelper.WriteLine($"  Detected corners: TL=({detectedCorners.TopLeft.X:F1},{detectedCorners.TopLeft.Y:F1}), " +
                                 $"TR=({detectedCorners.TopRight.X:F1},{detectedCorners.TopRight.Y:F1}), " +
                                 $"BR=({detectedCorners.BottomRight.X:F1},{detectedCorners.BottomRight.Y:F1}), " +
                                 $"BL=({detectedCorners.BottomLeft.X:F1},{detectedCorners.BottomLeft.Y:F1})");

            // Act - Crop the oriented rectangle with this permutation
            using var croppedImage = ImageCropping.CropOriented(sourceImage, permutedList);

            // Only publish the first permutation for visual inspection
            if (permutationCount == 1)
            {
                await _publisher.PublishAsync(croppedImage, "Cropped oriented rectangle");
            }

            // Assert - Verify corner colors and dimensions for each permutation
            VerifyCornerColors(croppedImage);
            VerifyImageDimensions(croppedImage, 200, 120); // Expected: width=|p-q|=200, height=width=120
        }

        _outputHelper.WriteLine($"Successfully tested {permutationCount} vertex permutations");
    }

    [Fact]
    public async Task UpwardTiltedOrientedRectangle_GeneratesCorrectGradients()
    {
        // Arrange - Create an upward-tilted rectangle
        var imageWidth = 400;
        var imageHeight = 300;
        var p = new PointF(50, 200);   // Bottom-left-ish
        var q = new PointF(350, 100);  // Top-right-ish (upward slope)
        var width = 60f; // Width perpendicular to p-q edge

        // Generate test image with gradient-filled oriented rectangle
        using var sourceImage = OrientedRectangleTestUtils.CreateGradientOrientedRectangle(
            imageWidth, imageHeight, p, q, width);
        await _publisher.PublishAsync(sourceImage, "Source image with upward-tilted oriented rectangle");

        // Calculate oriented rectangle vertices for cropping
        var vertices = OrientedRectangleTestUtils.CalculateOrientedRectangleVertices(p, q, width);
        var orientedRect = vertices.Select(v => ((double)v.X, (double)v.Y)).ToList();

        // Calculate expected dimensions: width = |p-q|, height = width
        var expectedWidth = (int)Math.Round(Math.Sqrt(Math.Pow(q.X - p.X, 2) + Math.Pow(q.Y - p.Y, 2)));
        var expectedHeight = (int)Math.Round(width);

        // Test all possible vertex orderings to ensure order independence
        int permutationCount = 0;
        foreach (var permutedVertices in Permute(orientedRect))
        {
            permutationCount++;
            var permutedList = permutedVertices.ToList();

            // Act - Crop the oriented rectangle with this permutation
            using var croppedImage = ImageCropping.CropOriented(sourceImage, permutedList);

            // Only publish the first permutation for visual inspection
            if (permutationCount == 1)
            {
                await _publisher.PublishAsync(croppedImage, "Cropped upward-tilted oriented rectangle");
            }

            // Assert - Verify corner colors and dimensions for each permutation
            VerifyCornerColors(croppedImage);
            VerifyImageDimensions(croppedImage, expectedWidth, expectedHeight);
        }

        _outputHelper.WriteLine($"Successfully tested {permutationCount} vertex permutations");
    }

    [Fact]
    public async Task DownwardTiltedOrientedRectangle_GeneratesCorrectGradients()
    {
        // Arrange - Create a downward-tilted rectangle
        var imageWidth = 400;
        var imageHeight = 300;
        var p = new PointF(50, 80);    // Top-left-ish
        var q = new PointF(350, 220);  // Bottom-right-ish (downward slope)
        var width = 60f; // Width perpendicular to p-q edge

        // Generate test image with gradient-filled oriented rectangle
        using var sourceImage = OrientedRectangleTestUtils.CreateGradientOrientedRectangle(
            imageWidth, imageHeight, p, q, width);
        await _publisher.PublishAsync(sourceImage, "Source image with downward-tilted oriented rectangle");

        // Calculate oriented rectangle vertices for cropping
        var vertices = OrientedRectangleTestUtils.CalculateOrientedRectangleVertices(p, q, width);
        var orientedRect = vertices.Select(v => ((double)v.X, (double)v.Y)).ToList();

        // Calculate expected dimensions: width = |p-q|, height = width
        var expectedWidth = (int)Math.Round(Math.Sqrt(Math.Pow(q.X - p.X, 2) + Math.Pow(q.Y - p.Y, 2)));
        var expectedHeight = (int)Math.Round(width);

        // Test all possible vertex orderings to ensure order independence
        int permutationCount = 0;
        foreach (var permutedVertices in Permute(orientedRect))
        {
            permutationCount++;
            var permutedList = permutedVertices.ToList();

            // Act - Crop the oriented rectangle with this permutation
            using var croppedImage = ImageCropping.CropOriented(sourceImage, permutedList);

            // Only publish the first permutation for visual inspection
            if (permutationCount == 1)
            {
                await _publisher.PublishAsync(croppedImage, "Cropped downward-tilted oriented rectangle");
            }

            // Assert - Verify corner colors and dimensions for each permutation
            VerifyCornerColors(croppedImage);
            VerifyImageDimensions(croppedImage, expectedWidth, expectedHeight);
        }

        _outputHelper.WriteLine($"Successfully tested {permutationCount} vertex permutations");
    }

    /// <summary>
    /// Verifies that the cropped image has the expected corner colors for properly oriented text.
    /// Samples pixels 2 pixels inside each corner to account for potential background bleeding.
    /// </summary>
    private void VerifyCornerColors(Image<Rgb24> croppedImage)
    {
        var width = croppedImage.Width;
        var height = croppedImage.Height;

        // Sample points 2 pixels inside each corner
        var topLeft = croppedImage[2, 2];
        var topRight = croppedImage[width - 3, 2];
        var bottomRight = croppedImage[width - 3, height - 3];
        var bottomLeft = croppedImage[2, height - 3];

        _outputHelper.WriteLine($"Corner colors - TL: {topLeft}, TR: {topRight}, BR: {bottomRight}, BL: {bottomLeft}");

        // For properly oriented text, we expect:
        // Top-left: Red=0, Green=0 (origin of text)
        // Top-right: Red=0, Green=255 (end of text line)
        // Bottom-right: Red=255, Green=255 (bottom-right of text block)
        // Bottom-left: Red=255, Green=0 (bottom-left of text block)

        Assert.True(topLeft.R < 50, $"Top-left should have low red, got {topLeft.R}");
        Assert.True(topLeft.G < 50, $"Top-left should have low green, got {topLeft.G}");

        Assert.True(topRight.R < 50, $"Top-right should have low red, got {topRight.R}");
        Assert.True(topRight.G > 200, $"Top-right should have high green, got {topRight.G}");

        Assert.True(bottomRight.R > 200, $"Bottom-right should have high red, got {bottomRight.R}");
        Assert.True(bottomRight.G > 200, $"Bottom-right should have high green, got {bottomRight.G}");

        Assert.True(bottomLeft.R > 200, $"Bottom-left should have high red, got {bottomLeft.R}");
        Assert.True(bottomLeft.G < 50, $"Bottom-left should have low green, got {bottomLeft.G}");
    }

    /// <summary>
    /// Verifies that the cropped image dimensions are within ±2 pixels of expected values.
    /// </summary>
    private void VerifyImageDimensions(Image<Rgb24> croppedImage, int expectedWidth, int expectedHeight)
    {
        var actualWidth = croppedImage.Width;
        var actualHeight = croppedImage.Height;

        _outputHelper.WriteLine($"Image dimensions - Expected: {expectedWidth}x{expectedHeight}, Actual: {actualWidth}x{actualHeight}");

        Assert.True(Math.Abs(actualWidth - expectedWidth) <= 2,
            $"Width should be within ±2 of {expectedWidth}, got {actualWidth}");
        Assert.True(Math.Abs(actualHeight - expectedHeight) <= 2,
            $"Height should be within ±2 of {expectedHeight}, got {actualHeight}");
    }

    /// <summary>
    /// Generates all permutations of a collection of items.
    /// </summary>
    private static IEnumerable<IEnumerable<T>> Permute<T>(IEnumerable<T> sequence)
    {
        var items = sequence.ToArray();
        if (items.Length == 0)
        {
            yield return Enumerable.Empty<T>();
        }
        else
        {
            for (int i = 0; i < items.Length; i++)
            {
                var remaining = items.Take(i).Concat(items.Skip(i + 1));
                foreach (var permutation in Permute(remaining))
                {
                    yield return new[] { items[i] }.Concat(permutation);
                }
            }
        }
    }
}
