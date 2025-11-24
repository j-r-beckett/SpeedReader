// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Frontend;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ocr.Geometry;
using Ocr.InferenceEngine;
using Ocr.InferenceEngine.Engines;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TestUtils;
using Xunit.Abstractions;

namespace Ocr.Test.E2E;

public class TextDetectorE2ETests
{
    private readonly TestLogger _logger;

    public TextDetectorE2ETests(ITestOutputHelper outputHelper) => _logger = new TestLogger(outputHelper);

    [Fact]
    public async Task Detection_ReturnsCorrectResults_StraightText()
    {
        // Arrange
        var expected = Utils.CreateTestImage(720, 640, [
            ("undertake", 100, 400, 0),
            ("yup", 300, 150, 0),
            ("anabranch", 500, 100, 0),
            ("happy", 600, 200, 0)
        ]);

        // Act
        var actual = await RunDetection(expected.Image, expected.Detections);

        // Assert
        Utils.ValidateDetections(expected.Detections, actual);
    }

    [Fact]
    public async Task Detection_ReturnsCorrectResults_RotatedText()
    {
        // Arrange
        var expected = Utils.CreateTestImage(720, 640, [
            ("dastardly", 450, 450, 35),
            ("citizen", 300, 150, 15),
            ("nowhere", 600, 100, 50),
            ("guppy", 150, 500, -45)
        ]);

        // Act
        var actual = await RunDetection(expected.Image, expected.Detections);

        // Assert
        Utils.ValidateDetections(expected.Detections, actual);
    }

    [Fact]
    public async Task Detection_ReturnsCorrectResults_NoText()
    {
        // Arrange
        var expected = Utils.CreateTestImage(720, 640, []);

        // Act
        var actual = await RunDetection(expected.Image, expected.Detections);

        // Assert
        Utils.ValidateDetections(expected.Detections, actual);
    }

    private async Task<List<BoundingBox>> RunDetection(Image<Rgb24> image, List<BoundingBox> expectedBBoxes)
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
                    numIntraOpThreads: 4),
                Parallelism = 1
            },
            RecognitionEngine = new CpuEngineConfig
            {
                Kernel = new OnnxInferenceKernelOptions(
                    model: Model.Svtr,
                    quantization: Quantization.Fp32,
                    numIntraOpThreads: 1),
                Parallelism = 1
            }
        };

        var services = new ServiceCollection();
        services.AddOcrPipeline(options);
        var provider = services.BuildServiceProvider();
        var detector = provider.GetRequiredService<TextDetector>();

        var vizBuilder = new VizBuilder();
        var results = await detector.Detect(image, vizBuilder);

        var svg = vizBuilder
            .AddExpectedAxisAlignedBBoxes(expectedBBoxes.Select(b => b.AxisAlignedRectangle).ToList())
            .AddExpectedOrientedBBoxes(expectedBBoxes.Select(b => b.RotatedRectangle).ToList(), true)
            .RenderSvg();

        _logger.LogInformation($"Saved visualization to {await svg.SaveAsDataUri()}");

        return results;
    }

}
