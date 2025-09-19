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
    public async Task ReadOne_ReturnsCorrectResult()
    {
        using var image = new Image<Rgb24>(720, 640, Color.White);

        const string text = "greetings";

        var bbox = Utils.DrawText(image, text, 200, 200);

        var reader = CreateTextReader();

        var (results, vizBuilder) = await await reader.ReadOne(image);

        var svg = vizBuilder.RenderSvg();
        _logger.LogInformation($"Saved visualization to {await svg.SaveAsDataUri()}");

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

    [Fact]
    public async Task ReadMany_ReturnsCorrectResults()
    {

        using var image1 = new Image<Rgb24>(720, 640, Color.White);
        const string text1 = "yanked";
        var bbox1 = Utils.DrawText(image1, text1, 200, 200);

        using var image2 = new Image<Rgb24>(500, 800, Color.White);
        const string text2 = "Kazakhstan";
        var bbox2 = Utils.DrawText(image2, text2, 300, 250);

        using var image3 = new Image<Rgb24>(800, 600, Color.White);
        const string text3 = "specimen";
        var bbox3 = Utils.DrawText(image3, text3, 500, 100);

        var reader = CreateTextReader();

        List<(Image<Rgb24> Image, List<(double, double)> BBox, string Text)> cases =
            [
                (image1, bbox1, text1),
                (image2, bbox2, text2),
                (image3, bbox3, text3)
            ];

        var images = cases.Select(c => c.Image).ToAsyncEnumerable();

        var i = 0;
        await foreach (var item in reader.ReadMany(images))
        {
            var svg = item.VizBuilder.RenderSvg();
            _logger.LogInformation($"Saved visualization to {await svg.SaveAsDataUri()}");

            var results = item.Result;
            var text = cases[i].Text;
            var bbox = cases[i++].BBox;

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

        var factory = () => (new TextDetector(dbnetRunner), new TextRecognizer(svtrRunner));

        return new SpeedReader(factory, 1, 1);
    }
}
