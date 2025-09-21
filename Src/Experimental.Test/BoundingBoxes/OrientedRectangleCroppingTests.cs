// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Experimental.BoundingBoxes;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TestUtils;
using Xunit.Abstractions;
using BoundingBoxPointF = Experimental.BoundingBoxes.PointF;

namespace Experimental.Test.BoundingBoxes;

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
        var imageWidth = 400;
        var imageHeight = 300;
        var p = new SixLabors.ImageSharp.PointF(100, 80);
        var q = new SixLabors.ImageSharp.PointF(300, 80);
        var width = 120f;

        using var sourceImage = OrientedRectangleTestUtils.CreateGradientOrientedRectangle(
            imageWidth, imageHeight, p, q, width);
        await _publisher.PublishAsync(sourceImage, "Source image with axis-aligned oriented rectangle");

        var vertices = OrientedRectangleTestUtils.CalculateOrientedRectangleVertices(p, q, width);
        var corners = vertices.Select(v => new BoundingBoxPointF { X = v.X, Y = v.Y }).ToList();

        _outputHelper.WriteLine($"Corners: [{string.Join(", ", corners.Select(v => $"({v.X:F1},{v.Y:F1})"))}]");

        var rotatedRect = corners.ToRotatedRectangle();
        var detectedCorners = rotatedRect.Corners();
        _outputHelper.WriteLine($"Detected corners: [{string.Join(", ", detectedCorners.Select(c => $"({c.X:F1},{c.Y:F1})"))}]");

        using var croppedImage = sourceImage.Crop(rotatedRect);
        await _publisher.PublishAsync(croppedImage, "Cropped oriented rectangle");

        VerifyCornerColors(croppedImage);
        VerifyImageDimensions(croppedImage, 200, 120);
    }

    [Fact]
    public async Task UpwardTiltedOrientedRectangle_GeneratesCorrectGradients()
    {
        var imageWidth = 400;
        var imageHeight = 300;
        var p = new SixLabors.ImageSharp.PointF(50, 200);
        var q = new SixLabors.ImageSharp.PointF(350, 100);
        var width = 60f;

        using var sourceImage = OrientedRectangleTestUtils.CreateGradientOrientedRectangle(
            imageWidth, imageHeight, p, q, width);
        await _publisher.PublishAsync(sourceImage, "Source image with upward-tilted oriented rectangle");

        var vertices = OrientedRectangleTestUtils.CalculateOrientedRectangleVertices(p, q, width);
        var corners = vertices.Select(v => new BoundingBoxPointF { X = v.X, Y = v.Y }).ToList();

        var expectedWidth = (int)Math.Round(Math.Sqrt(Math.Pow(q.X - p.X, 2) + Math.Pow(q.Y - p.Y, 2)));
        var expectedHeight = (int)Math.Round(width);

        var rotatedRect = corners.ToRotatedRectangle();
        using var croppedImage = sourceImage.Crop(rotatedRect);
        await _publisher.PublishAsync(croppedImage, "Cropped upward-tilted oriented rectangle");

        VerifyCornerColors(croppedImage);
        VerifyImageDimensions(croppedImage, expectedWidth, expectedHeight);
    }

    [Fact]
    public async Task DownwardTiltedOrientedRectangle_GeneratesCorrectGradients()
    {
        var imageWidth = 400;
        var imageHeight = 300;
        var p = new SixLabors.ImageSharp.PointF(50, 80);
        var q = new SixLabors.ImageSharp.PointF(350, 220);
        var width = 60f;

        using var sourceImage = OrientedRectangleTestUtils.CreateGradientOrientedRectangle(
            imageWidth, imageHeight, p, q, width);
        await _publisher.PublishAsync(sourceImage, "Source image with downward-tilted oriented rectangle");

        var vertices = OrientedRectangleTestUtils.CalculateOrientedRectangleVertices(p, q, width);
        var corners = vertices.Select(v => new BoundingBoxPointF { X = v.X, Y = v.Y }).ToList();

        var expectedWidth = (int)Math.Round(Math.Sqrt(Math.Pow(q.X - p.X, 2) + Math.Pow(q.Y - p.Y, 2)));
        var expectedHeight = (int)Math.Round(width);

        var rotatedRect = corners.ToRotatedRectangle();
        using var croppedImage = sourceImage.Crop(rotatedRect);
        await _publisher.PublishAsync(croppedImage, "Cropped downward-tilted oriented rectangle");

        VerifyCornerColors(croppedImage);
        VerifyImageDimensions(croppedImage, expectedWidth, expectedHeight);
    }

    private void VerifyCornerColors(Image<Rgb24> croppedImage)
    {
        var width = croppedImage.Width;
        var height = croppedImage.Height;

        var inset = 3;

        var topLeft = croppedImage[inset, inset];
        var topRight = croppedImage[width - inset, inset];
        var bottomRight = croppedImage[width - inset, height - inset];
        var bottomLeft = croppedImage[inset, height - inset];

        _outputHelper.WriteLine($"Corner colors - TL: {topLeft}, TR: {topRight}, BR: {bottomRight}, BL: {bottomLeft}");

        Assert.True(topLeft.R < 50, $"Top-left should have low red, got {topLeft.R}");
        Assert.True(topLeft.G < 50, $"Top-left should have low green, got {topLeft.G}");

        Assert.True(topRight.R < 50, $"Top-right should have low red, got {topRight.R}");
        Assert.True(topRight.G > 200, $"Top-right should have high green, got {topRight.G}");

        Assert.True(bottomRight.R > 200, $"Bottom-right should have high red, got {bottomRight.R}");
        Assert.True(bottomRight.G > 200, $"Bottom-right should have high green, got {bottomRight.G}");

        Assert.True(bottomLeft.R > 200, $"Bottom-left should have high red, got {bottomLeft.R}");
        Assert.True(bottomLeft.G < 50, $"Bottom-left should have low green, got {bottomLeft.G}");
    }

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

    [Fact]
    public void CropOriented_WithSquareOrientedRectangle_ShouldCropSuccessfully()
    {
        var squareCorners = new List<BoundingBoxPointF>
        {
            new() { X = 100.0, Y = 100.0 },
            new() { X = 200.0, Y = 100.0 },
            new() { X = 200.0, Y = 200.0 },
            new() { X = 100.0, Y = 200.0 }
        };

        using var testImage = new Image<Rgb24>(300, 300);
        var rotatedRect = squareCorners.ToRotatedRectangle();
        using var croppedImage = testImage.Crop(rotatedRect);

        Assert.Equal(100, croppedImage.Width);
        Assert.Equal(100, croppedImage.Height);
    }

}
