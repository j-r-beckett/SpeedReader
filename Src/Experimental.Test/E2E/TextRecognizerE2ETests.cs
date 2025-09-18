// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Core;
using Experimental.Inference;
using Microsoft.ML.OnnxRuntime;
using Ocr;
using Resources;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TestUtils;
using Xunit.Abstractions;

namespace Experimental.Test.E2E;

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
        using var image = new Image<Rgb24>(720, 640, Color.White);

        var bbox = DrawText(image, text, 100, 100);

        var (actualText, confidence) = await RunRecognition(image, bbox);

        Assert.Equal(text, actualText);
        Assert.True(confidence >= 0.98);
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
        using var image = new Image<Rgb24>(720, 640, Color.White);

        const string text = "greetings";

        var bbox = DrawText(image, text, 100, 100, angleDegrees);

        var (actualText, confidence) = await RunRecognition(image, bbox);

        Assert.Equal(text, actualText);
        Assert.True(confidence >= 0.98);
    }

    [Fact]
    public async Task ReturnsCorrectResult_NoText()
    {
        using var image = new Image<Rgb24>(720, 640, Color.White);

        List<(double, double)> bbox = [(100, 100), (200, 100), (200, 200), (100, 200)];

        var (actualText, confidence) = await RunRecognition(image, bbox);

        Assert.Equal(string.Empty, actualText);
        Assert.True(confidence <= 0.01);
    }

    private async Task<(string Text, double Confidence)> RunRecognition(Image<Rgb24> image, List<(double X, double Y)> bbox)
    {
        var session = _modelProvider.GetSession(Model.SVTRv2);
        var svtrRunner = new CpuModelRunner(session, 1);
        var recognizer = new TextRecognizer(svtrRunner);
        return await recognizer.Recognize(bbox, image);
    }

    private List<(double X, double Y)> DrawText(Image image, string text, int x, int y, float angleDegrees = 0)
    {
        var textRect = TextMeasurer.MeasureAdvance(text, new TextOptions(_font));
        var width = Math.Ceiling(textRect.Width);
        var height = Math.Ceiling(textRect.Height);

        // Draw text, rotated angleDegrees counterclockwise around (x, y)
        image.Mutate(ctx => ctx
            .SetDrawingTransform(Matrix3x2Extensions.CreateRotationDegrees(-angleDegrees, new PointF(x, y)))
            .DrawText(text, _font, Color.Black, new PointF(x, y)));

        // Calculate oriented rectangle; start with 4 corners outlining a rectangle at the origin
        List<(double X, double Y)> corners =
        [
            (0, 0),           // Top-left
            (width, 0),       // Top-right
            (width, height),  // Bottom-right
            (0, height)       // Bottom-left
        ];

        // Rotate the rectangle angleDegrees around the origin, then translate to (x, y)
        var angleRadians = -angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(angleRadians);
        var sin = Math.Sin(angleRadians);

        return corners.Select(corner =>
        {
            var rotatedX = corner.X * cos - corner.Y * sin + x;
            var rotatedY = corner.X * sin + corner.Y * cos + y;
            return (rotatedX, rotatedY);
        }).ToList();
    }
}
