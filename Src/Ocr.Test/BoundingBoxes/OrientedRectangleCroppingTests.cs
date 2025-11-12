// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Logging;
using Ocr.Geometry;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TestUtils;
using Xunit.Abstractions;
using BoundingBoxPointF = Ocr.Geometry.PointF;

namespace Ocr.Test.BoundingBoxes;

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

        var rotatedRect = new RotatedRectangle(corners);
        var detectedCorners = rotatedRect.Corners().Points;
        _outputHelper.WriteLine($"Detected corners: [{string.Join(", ", detectedCorners.Select(c => $"({c.X:F1},{c.Y:F1})"))}]");

        using var croppedImage = rotatedRect.Crop(sourceImage);
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

        var rotatedRect = new RotatedRectangle(corners);
        using var croppedImage = rotatedRect.Crop(sourceImage);
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

        var rotatedRect = new RotatedRectangle(corners);
        using var croppedImage = rotatedRect.Crop(sourceImage);
        await _publisher.PublishAsync(croppedImage, "Cropped downward-tilted oriented rectangle");

        VerifyCornerColors(croppedImage);
        VerifyImageDimensions(croppedImage, expectedWidth, expectedHeight);
    }

    private void VerifyCornerColors(Image<Rgb24> croppedImage)
    {
        var width = croppedImage.Width;
        var height = croppedImage.Height;

        var inset = 5;

        var topLeft = croppedImage[inset, inset];
        var topRight = croppedImage[width - inset, inset];
        var bottomRight = croppedImage[width - inset, height - inset];
        var bottomLeft = croppedImage[inset, height - inset];

        _outputHelper.WriteLine($"Corner colors - TL: {topLeft}, TR: {topRight}, BR: {bottomRight}, BL: {bottomLeft}");

        var tolerance = 50;

        Assert.True(topLeft.R < tolerance, $"Top-left should have low red, got {topLeft.R}");
        Assert.True(topLeft.G < 255 - tolerance, $"Top-left should have low green, got {topLeft.G}");

        Assert.True(topRight.R < tolerance, $"Top-right should have low red, got {topRight.R}");
        Assert.True(topRight.G > 255 - tolerance, $"Top-right should have high green, got {topRight.G}");

        Assert.True(bottomRight.R > 255 - tolerance, $"Bottom-right should have high red, got {bottomRight.R}");
        Assert.True(bottomRight.G > 255 - tolerance, $"Bottom-right should have high green, got {bottomRight.G}");

        Assert.True(bottomLeft.R > 255 - tolerance, $"Bottom-left should have high red, got {bottomLeft.R}");
        Assert.True(bottomLeft.G < tolerance, $"Bottom-left should have low green, got {bottomLeft.G}");
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
    public async Task CropOriented_WithRandomOrientedRectangles_GeneratesCorrectCrops()
    {
        var random = new Random(0);
        var numIterations = 100;

        for (int n = 0; n < numIterations; n++)
        {
            var imageWidth = 700;
            var imageHeight = 600;

            var centerX = 200 + random.NextDouble() * 200;
            var centerY = 160 + random.NextDouble() * 140;
            var rectHeight = 50 + random.NextDouble() * 100;
            var rectWidth = rectHeight + 50 + random.NextDouble() * 100;
            var angleDegrees = (random.NextDouble() - 0.5) * 180;

            using var sourceImage = OrientedRectangleTestUtils.CreateRotatedRectangleTest(
                imageWidth, imageHeight, (float)centerX, (float)centerY,
                (float)rectWidth, (float)rectHeight, (float)angleDegrees);

            await _publisher.PublishAsync(sourceImage, $"Random source image {n} - {angleDegrees:F1}° rotation");

            var angleRadians = angleDegrees * Math.PI / 180.0;
            var cos = Math.Cos(angleRadians);
            var sin = Math.Sin(angleRadians);

            var halfWidth = rectWidth / 2;
            var halfHeight = rectHeight / 2;

            var corners = new List<BoundingBoxPointF>
            {
                new() { X = centerX + (-halfWidth * cos - -halfHeight * sin), Y = centerY + (-halfWidth * sin + -halfHeight * cos) },
                new() { X = centerX + (halfWidth * cos - -halfHeight * sin), Y = centerY + (halfWidth * sin + -halfHeight * cos) },
                new() { X = centerX + (halfWidth * cos - halfHeight * sin), Y = centerY + (halfWidth * sin + halfHeight * cos) },
                new() { X = centerX + (-halfWidth * cos - halfHeight * sin), Y = centerY + (-halfWidth * sin + halfHeight * cos) }
            };

            _outputHelper.WriteLine($"Iteration {n}: Center=({centerX:F1},{centerY:F1}), Size={rectWidth:F1}x{rectHeight:F1}, Angle={angleDegrees:F1}°");

            var rotatedRect = new RotatedRectangle(corners);
            using var croppedImage = rotatedRect.Crop(sourceImage);

            await _publisher.PublishAsync(croppedImage, $"Random cropped image {n}");

            VerifyCornerColors(croppedImage);
            VerifyImageDimensions(croppedImage, (int)Math.Round(rectWidth), (int)Math.Round(rectHeight));
        }

        _outputHelper.WriteLine($"Successfully tested {numIterations} random oriented rectangle crops");
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
        var rotatedRect = new RotatedRectangle(squareCorners);
        using var croppedImage = rotatedRect.Crop(testImage);

        Assert.Equal(100, croppedImage.Width);
        Assert.Equal(100, croppedImage.Height);
    }

}
