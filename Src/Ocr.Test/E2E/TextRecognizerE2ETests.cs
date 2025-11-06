// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Immutable;
using System.Diagnostics;
using Core;
using Microsoft.Extensions.Logging;
using Ocr.Geometry;
using Ocr.Inference;
using Ocr.Visualization;
using Resources;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TestUtils;
using Xunit.Abstractions;

namespace Ocr.Test.E2E;

public class TextRecognizerE2ETests
{
    private readonly ModelProvider _modelProvider;
    private readonly Font _font;
    private readonly TestLogger _logger;

    public TextRecognizerE2ETests(ITestOutputHelper outputHelper)
    {
        _modelProvider = new ModelProvider();
        _font = Fonts.GetFont(fontSize: 24f);
        _logger = new TestLogger(outputHelper);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("CapitalLetters")]
    [InlineData("hyphens-are-fun")]
    [InlineData("period.")]
    [InlineData("two words")]
    public async Task ReturnsCorrectResult_StraightText(string text)
    {
        // Arrange
        using var image = new Image<Rgb24>(720, 640, Color.White);

        var bbox = Utils.DrawText(image, text, 100, 100);

        // Act
        var (actualText, confidence) = await RunRecognition(image, bbox.RotatedRectangle);

        // Assert
        Assert.Equal(text, actualText);
        Assert.True(confidence >= 0.98);  // If this fails, there's a bug. Do not tweak to make test pass
    }

    [Theory]
    [InlineData(15)]
    [InlineData(45)]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(-15)]
    [InlineData(-45)]
    [InlineData(-60)]
    [InlineData(-90)]
    public async Task ReturnsCorrectResult_AngledText(int angleDegrees)
    {
        // Arrange
        using var image = new Image<Rgb24>(720, 640, Color.White);

        const string text = "greetings";

        var bbox = Utils.DrawText(image, text, 100, 100, angleDegrees);

        // Act
        var (actualText, confidence) = await RunRecognition(image, bbox.RotatedRectangle);

        // Assert
        Assert.Equal(text, actualText);
        Assert.True(confidence >= 0.98);  // If this fails, there's a bug. Do not tweak to make test pass
    }

    [Fact]
    public async Task ReturnsCorrectResult_NoText()
    {
        // Arrange
        using var image = new Image<Rgb24>(720, 640, Color.White);

        RotatedRectangle bbox = new() { X = 100, Y = 100, Width = 100, Height = 100, Angle = 0 };

        // Act
        var (actualText, confidence) = await RunRecognition(image, bbox);

        // Assert
        Assert.Equal(string.Empty, actualText);
        Assert.True(confidence <= 0.01);
    }

    private async Task<(string Text, double Confidence)> RunRecognition(Image<Rgb24> image, RotatedRectangle rect)
    {
        var session = _modelProvider.GetSession(Model.SVTRv2);
        var svtrRunner = new CpuModelRunner(session, 1);
        var vizBuilder = new VizBuilder();
        vizBuilder.AddBaseImage(image);
        vizBuilder.AddOrientedBBoxes([rect], true);
        var recognizer = new TextRecognizer(svtrRunner);
        var polygon = new Polygon
        {
            Points = rect.Corners().Select(p => (Geometry.Point)p).ToImmutableList()
        };
        var bbox = new BoundingBox(polygon, rect);
        var result = await recognizer.Recognize([bbox], image, vizBuilder);

        var svg = vizBuilder.RenderSvg();
        _logger.LogInformation($"Saved visualization to {await svg.SaveAsDataUri()}");

        Debug.Assert(result.Count == 1);
        return result[0];
    }
}
