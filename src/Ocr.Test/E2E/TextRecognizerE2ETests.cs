// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SpeedReader.Ocr.Geometry;
using SpeedReader.Ocr.InferenceEngine;
using SpeedReader.Ocr.Visualization;
using SpeedReader.Resources.Font;
using SpeedReader.TestUtils;
using Xunit.Abstractions;

namespace SpeedReader.Ocr.Test.E2E;

public class TextRecognizerE2ETests
{
    private readonly Font _font;
    private readonly TestLogger _logger;

    public TextRecognizerE2ETests(ITestOutputHelper outputHelper)
    {
        _font = EmbeddedFont.Default.Get(fontSize: 24f);
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
        var options = new OcrPipelineOptions
        {
            DetectionOptions = new DetectionOptions(),
            RecognitionOptions = new RecognitionOptions(),
            DetectionEngine = new CpuEngineConfig
            {
                Kernel = new OnnxInferenceKernelOptions(
                    model: Model.DbNet,
                    quantization: Quantization.Int8,
                    numIntraOpThreads: 1),
                Parallelism = 1
            },
            RecognitionEngine = new CpuEngineConfig
            {
                Kernel = new OnnxInferenceKernelOptions(
                    model: Model.Svtr,
                    quantization: Quantization.Fp32,
                    numIntraOpThreads: 4),
                Parallelism = 1
            }
        };

        var services = new ServiceCollection();
        services.AddOcrPipeline(options);
        var provider = services.BuildServiceProvider();
        var recognizer = provider.GetRequiredService<TextRecognizer>();

        var vizBuilder = new VizBuilder();
        vizBuilder.AddBaseImage(image);
        vizBuilder.AddOrientedBBoxes([rect], true);

        var bbox = new BoundingBox
        {
            Polygon = rect.Corners(),
            RotatedRectangle = rect,
            AxisAlignedRectangle = rect.ToAxisAlignedRectangle()
        };
        var result = await recognizer.Recognize([bbox], image, vizBuilder);

        var svg = vizBuilder.RenderSvg();
        _logger.LogInformation($"Saved visualization to {await svg.SaveAsDataUri()}");

        Debug.Assert(result.Count == 1);
        return result[0];
    }
}
