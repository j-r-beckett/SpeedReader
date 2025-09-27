// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Core;
using Experimental.Geometry;
using Experimental.Inference;
using Experimental.Visualization;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TestUtils;
using Xunit.Abstractions;

namespace Experimental.Test.E2E;

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
        using var image = new Image<Rgb24>(720, 640, Color.White);

        List<BoundingBox> expected =
        [
            Utils.DrawText(image, "undertake", 100, 400),
            Utils.DrawText(image, "yup", 300, 150),
            Utils.DrawText(image, "anabranch", 500, 100),
            Utils.DrawText(image, "happy", 600, 200),
        ];

        // Act
        var actual = await RunDetection(image, expected);

        // Assert
        Utils.ValidateDetections(expected, actual);
    }

    [Fact]
    public async Task Detection_ReturnsCorrectResults_RotatedText()
    {
        // Arrange
        using var image = new Image<Rgb24>(720, 640, Color.White);

        List<BoundingBox> expected =
        [
            Utils.DrawText(image, "dastardly", 450, 450, 35),
            Utils.DrawText(image, "citizen", 300, 150, 15),
            Utils.DrawText(image, "nowhere", 600, 100, 50),
            Utils.DrawText(image, "guppy", 150, 500, -45),
        ];

        // Act
        var actual = await RunDetection(image, expected);

        // Assert
        Utils.ValidateDetections(expected, actual);
    }

    [Fact]
    public async Task Detection_ReturnsCorrectResults_NoText()
    {
        // Arrange
        using var image = new Image<Rgb24>(720, 640, Color.White);

        List<BoundingBox> expected = [];

        // Act
        var actual = await RunDetection(image, expected);

        // Assert
        Utils.ValidateDetections(expected, actual);
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
