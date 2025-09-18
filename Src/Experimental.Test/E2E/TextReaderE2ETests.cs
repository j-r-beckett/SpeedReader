// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Core;
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
    public async Task ReturnsCorrectResult()
    {
        using var image = new Image<Rgb24>(720, 640, Color.White);

        const string text = "greetings";

        var bbox = Utils.DrawText(image, text, 200, 200);

        var results = await Ocr(image);

        Assert.Single(results);

        var bboxes = results.Select(r => r.BBox).ToList();
        var axisAlignedBBoxes = bboxes.Select(d => d.AARectangle).ToList();
        var orientedBBoxes = bboxes.Select(d => d.ORectangle).ToList();
        var polygonBBoxes = bboxes.Select(d => d.Polygon).ToList();

        Utils.ValidateAxisAlignedBBoxes([Utils.ToAxisAlignedRectangle(bbox)], axisAlignedBBoxes);
        Utils.ValidateOrientedBBoxes([bbox], orientedBBoxes);
        foreach (var polygon in polygonBBoxes)
        {
            Assert.True(polygon.Count >= 4);
        }

        Assert.True(results[0].Text == text);
    }

    private async Task<List<(TextBoundary BBox, string Text, double Confidence)>> Ocr(Image<Rgb24> image)
    {
        var dbnetSession = _modelProvider.GetSession(Model.DbNet18, ModelPrecision.INT8, new SessionOptions
        {
            IntraOpNumThreads = 4
        });
        var dbnetRunner = new CpuModelRunner(dbnetSession, 1);

        var svtrSession = _modelProvider.GetSession(Model.SVTRv2);
        var svtrRunner = new CpuModelRunner(svtrSession, 1);

        var vizBuilder = new VizBuilder();

        var detector = new TextDetector(dbnetRunner, vizBuilder);
        var recognizer = new TextRecognizer(svtrRunner, vizBuilder);

        var reader = new TextReader(() => (detector, recognizer), 1, 1);

        var result = await await reader.ReadOne(image);

        var svg = vizBuilder.RenderSvg();
        _logger.LogInformation($"Saved visualization to {await svg.SaveAsDataUri()}");

        return result;
    }
}
