// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Core;
using Experimental.Geometry;
using Experimental.Inference;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Ocr;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TestUtils;
using Xunit.Abstractions;

namespace Experimental.Test.E2E;

public class TextReaderE2ETests
{
    private readonly ModelProvider _modelProvider;
    private readonly TestLogger _logger;

    public TextReaderE2ETests(ITestOutputHelper outputHelper)
    {
        _modelProvider = new ModelProvider();
        _logger = new TestLogger(outputHelper);
    }

    [Fact]
    public async Task ReadOne_ReturnsCorrectResult()
    {
        // Arrange
        using var image = new Image<Rgb24>(720, 640, Color.White);

        const string expectedText = "greetings";

        var expectedBBox = Utils.DrawText(image, expectedText, 200, 200);

        var expectedResult = new SpeedReaderResult(image, [expectedBBox], [(expectedText, 1.0)], null!);

        // Act
        var actualResult = await ReadOne(image);

        // Assert
        Utils.ValidateDetectionsAndRecognitions(expectedResult, actualResult);
    }

    [Fact]
    public async Task ReadMany_ReturnsCorrectResults()
    {
        // Arrange
        using var image1 = new Image<Rgb24>(720, 640, Color.White);
        const string text1 = "yanked";
        var bbox1 = Utils.DrawText(image1, text1, 200, 200);

        using var image2 = new Image<Rgb24>(500, 800, Color.White);
        const string text2 = "Kazakhstan";
        var bbox2 = Utils.DrawText(image2, text2, 300, 250);

        using var image3 = new Image<Rgb24>(800, 600, Color.White);
        const string text3 = "specimen";
        var bbox3 = Utils.DrawText(image3, text3, 500, 100);

        List<SpeedReaderResult> expectedResults =
        [
            new (image1, [bbox1], [(text1, 1.0)], null!),
            new (image2, [bbox2], [(text2, 1.0)], null!),
            new (image3, [bbox3], [(text3, 1.0)], null!)
        ];

        // Act
        var images = expectedResults.Select(r => r.Image).ToList();
        var actualResults = await ReadMany(images);

        // Assert
        Assert.Equal(expectedResults.Count, actualResults.Count);

        foreach (var (expected, actual) in expectedResults.Zip(actualResults))
        {
            Utils.ValidateDetectionsAndRecognitions(expected, actual);
        }
    }

    private async Task<SpeedReaderResult> ReadOne(Image<Rgb24> image)
    {
        var reader = CreateTextReader();

        var result = await await reader.ReadOne(image);

        var svg = result.VizBuilder.RenderSvg();
        _logger.LogInformation($"Saved visualization to {await svg.SaveAsDataUri()}");

        return result;
    }

    private async Task<List<SpeedReaderResult>> ReadMany(List<Image<Rgb24>> images)
    {
        var reader = CreateTextReader();

        var results = await reader.ReadMany(images.ToAsyncEnumerable()).ToListAsync();

        foreach (var result in results)
        {
            var svg = result.VizBuilder.RenderSvg();
            _logger.LogInformation($"Saved visualization to {await svg.SaveAsDataUri()}");
        }

        return results;
    }

    private SpeedReader CreateTextReader()
    {
        var dbnetSession = _modelProvider.GetSession(Model.DbNet18, ModelPrecision.INT8, new SessionOptions
        {
            IntraOpNumThreads = 4
        });
        var dbnetRunner = new CpuModelRunner(dbnetSession, 1);

        var svtrSession = _modelProvider.GetSession(Model.SVTRv2);
        var svtrRunner = new CpuModelRunner(svtrSession, 1);

        return new SpeedReader(dbnetRunner, svtrRunner, 1, 1);
    }
}
