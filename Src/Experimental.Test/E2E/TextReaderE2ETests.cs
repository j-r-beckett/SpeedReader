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
    public async Task ReadOne_ReturnsCorrectResult_WideImage()
    {
        // Arrange
        var expectedResult = Utils.CreateTestImage(5000, 300, [
            ("mountain", 100, 100, 15),
            ("dump", 1000, 150, -15),
            ("running", 2500, 120, 30),
            ("jumping", 4500, 200, -30)
        ]);

        // Act
        var actualResult = await ReadOne(expectedResult.Image);

        // Assert
        Utils.ValidateDetectionsAndRecognitions(expectedResult, actualResult);
    }

    [Fact]
    public async Task ReadOne_ReturnsCorrectResult_NarrowImage()
    {
        // Arrange
        var expectedResult = Utils.CreateTestImage(300, 5000, [
            ("mountain", 100, 100, 15),
            ("dump", 150, 1000, -15),
            ("running", 120, 2500, 30),
            ("jumping", 200, 4500, -30)
        ]);

        // Act
        var actualResult = await ReadOne(expectedResult.Image);

        // Assert
        Utils.ValidateDetectionsAndRecognitions(expectedResult, actualResult);
    }

    [Fact]
    public async Task ReadOne_ReturnsCorrectResult_SmallImage()
    {
        // Arrange
        var expectedResult = Utils.CreateTestImage(300, 150, [
            ("munge", 100, 100, 15),
            ("dump", 10, 30, -15),
            ("running", 50, 80, 30),
        ]);

        // Act
        var actualResult = await ReadOne(expectedResult.Image);

        // Assert
        Utils.ValidateDetectionsAndRecognitions(expectedResult, actualResult);
    }

    [Fact]
    public async Task ReadOne_ReturnsCorrectResult_StraightText()
    {
        // Arrange
        var expectedResult = Utils.CreateTestImage(720, 640, [
            ("mountain", 100, 400, 0),
            ("dump", 300, 150, 0),
            ("running", 500, 100, 0),
            ("jumping", 600, 200, 0)
        ]);

        // Act
        var actualResult = await ReadOne(expectedResult.Image);

        // Assert
        Utils.ValidateDetectionsAndRecognitions(expectedResult, actualResult);
    }

    [Fact]
    public async Task ReadOne_ReturnsCorrectResult_RotatedText()
    {
        // Arrange
        var expectedResult = Utils.CreateTestImage(720, 640, [
            ("zephyr", 450, 450, 35),
            ("kludge", 300, 150, 10),
            ("quixotic", 600, 100, 55),
            ("flummox", 150, 500, -40)
        ]);

        // Act
        var actualResult = await ReadOne(expectedResult.Image);

        // Assert
        Utils.ValidateDetectionsAndRecognitions(expectedResult, actualResult);
    }

    [Fact]
    public async Task ReadMany_ReturnsCorrectResults()
    {
        // Arrange
        var expectedResult1 = Utils.CreateTestImage(720, 640, [
            ("velcro", 200, 200, 0),
            ("jukebox", 400, 300, 15),
            ("gnome", 100, 500, 0)
        ]);
        var expectedResult2 = Utils.CreateTestImage(500, 800, [
            ("pickle", 300, 250, -15),
            ("wizard", 150, 600, 0)
        ]);
        var expectedResult3 = Utils.CreateTestImage(800, 600, [
            ("bamboo", 500, 100, 0),
            ("clockwork", 200, 400, 30),
            ("pretzels", 600, 300, -20),
            ("alpaca", 400, 500, 0)
        ]);

        List<SpeedReaderResult> expectedResults = [expectedResult1, expectedResult2, expectedResult3];

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
        // var options =  SessionOptions.MakeSessionOptionWithCudaProvider();
        var options = new SessionOptions { IntraOpNumThreads = 4 };
        var dbnetSession = _modelProvider.GetSession(Model.DbNet18, ModelPrecision.INT8, options);
        var dbnetRunner = new CpuModelRunner(dbnetSession, 1);

        var svtrSession = _modelProvider.GetSession(Model.SVTRv2);
        var svtrRunner = new CpuModelRunner(svtrSession, 1);

        return new SpeedReader(dbnetRunner, svtrRunner, 1, 1);
    }
}
