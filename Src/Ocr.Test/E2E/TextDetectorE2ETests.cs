// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Core;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Ocr.Geometry;
using Ocr.Inference;
using Ocr.Visualization;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TestUtils;
using Xunit.Abstractions;

namespace Ocr.Test.E2E;

public class TextDetectorE2ETests : IDisposable
{
    private readonly ModelProvider _modelProvider;
    private readonly TestLogger _logger;

    public TextDetectorE2ETests(ITestOutputHelper outputHelper)
    {
        _modelProvider = new ModelProvider();
        _logger = new TestLogger(outputHelper);
    }

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
        // Set IntraOpNumThreads to maximize throughput for non-parallelized CPU execution
        var session = _modelProvider.GetSession(Model.DbNet18, ModelPrecision.INT8, new SessionOptions
        {
            IntraOpNumThreads = 4
        });
        var dbnetRunner = new CpuModelRunner(session, 1);

        var vizBuilder = new VizBuilder();
        var detector = new TextDetector(dbnetRunner);

        var results = await detector.Detect(image, vizBuilder);

        var svg = vizBuilder
            .AddExpectedAxisAlignedBBoxes(expectedBBoxes.Select(b => b.AxisAlignedRectangle).ToList())
            .AddExpectedOrientedBBoxes(expectedBBoxes.Select(b => b.RotatedRectangle).ToList(), true)
            .RenderSvg();

        _logger.LogInformation($"Saved visualization to {await svg.SaveAsDataUri()}");

        return results;
    }

    public void Dispose()
    {
        _modelProvider.Dispose();
        GC.SuppressFinalize(this);
    }
}
