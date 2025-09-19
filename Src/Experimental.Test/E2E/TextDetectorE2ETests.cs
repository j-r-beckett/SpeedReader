// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Core;
using Experimental.Inference;
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
        using var image = new Image<Rgb24>(720, 640, Color.White);

        List<List<(double X, double Y)>> expected =
        [
            Utils.DrawText(image, "undertake", 100, 400),
            Utils.DrawText(image, "yup", 300, 150),
            Utils.DrawText(image, "anabranch", 500, 100),
            Utils.DrawText(image, "happy", 600, 200),
        ];

        await TestTextDetection(image, expected);
    }

    [Fact]
    public async Task Detection_ReturnsCorrectResults_RotatedText()
    {
        using var image = new Image<Rgb24>(720, 640, Color.White);

        List<List<(double X, double Y)>> expected =
        [
            Utils.DrawText(image, "dastardly", 450, 450, 35),
            Utils.DrawText(image, "citizen", 300, 150, 15),
            Utils.DrawText(image, "nowhere", 600, 100, 50),
            Utils.DrawText(image, "guppy", 150, 500, -45),
        ];

        await TestTextDetection(image, expected);
    }

    [Fact]
    public async Task Detection_ReturnsCorrectResults_NoText()
    {
        using var image = new Image<Rgb24>(720, 640, Color.White);

        List<List<(double X, double Y)>> expected = [];

        await TestTextDetection(image, expected);
    }

    private async Task TestTextDetection(Image<Rgb24> image, List<List<(double X, double Y)>> expectedBBoxes)
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

        var expectedAxisAligned = expectedBBoxes.Select(Utils.ToAxisAlignedRectangle).ToList();

        var svg = vizBuilder
            .AddExpectedAxisAlignedBBoxes(expectedAxisAligned)
            .AddExpectedOrientedBBoxes(expectedBBoxes, true)
            .RenderSvg();

        _logger.LogInformation($"Saved visualization to {await svg.SaveAsDataUri()}");

        Utils.ValidateOrientedBBoxes(expectedBBoxes, results.Select(r => r.ORectangle).ToList());
        Utils.ValidateAxisAlignedBBoxes(expectedAxisAligned, results.Select(r => r.AARectangle).ToList());

        foreach (var result in results)
        {
            Assert.True(result.Polygon.Count >= 4);
        }
    }

    public void Dispose()
    {
        _modelProvider.Dispose();
        GC.SuppressFinalize(this);
    }
}
