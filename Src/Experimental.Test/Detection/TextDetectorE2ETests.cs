// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Clipper2Lib;
using Core;
using Experimental.Inference;
using Microsoft.Extensions.Logging;
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

namespace Experimental.Test.Detection;

public class TextDetectorE2ETests : IDisposable
{
    private readonly ModelProvider _modelProvider;
    private readonly Font _font;
    private readonly TestLogger _logger;

    public TextDetectorE2ETests(ITestOutputHelper outputHelper)
    {
        _modelProvider = new ModelProvider();
        _font = Fonts.GetFont(fontSize: 24f);
        _logger = new TestLogger(outputHelper);
    }

    [Fact]
    public async Task Detection_ReturnsCorrectResults()
    {
        var dbnetRunner = new CpuModelRunner(_modelProvider.GetSession(Model.DbNet18), 1);

        var detector = new TextDetector(dbnetRunner);

        using var image = new Image<Rgb24>(1200, 800, Color.White);

        var expected = new List<Rectangle>
        {
            DrawText(image, "hello", 100, 100),
            DrawText(image, "world", 300, 150),
            DrawText(image, "quick", 800, 100),
            DrawText(image, "brown", 1000, 200),
            DrawText(image, "fox", 150, 500),
            DrawText(image, "jumps", 250, 650),
            DrawText(image, "over", 900, 550),
            DrawText(image, "lazy", 800, 700),
            DrawText(image, "dog", 600, 400),
        };

        var results = await detector.Detect(image);

        var svg = new VizBuilder()
            .AddBaseImage(image)
            .AddAxisAlignedBBoxes(results.Select(r => r.AARectangle).ToList())
            .AddOrientedBBoxes(results.Select(r => r.ORectangle).ToList(), true)
            .RenderSvg();

        _logger.LogInformation($"Saved visualization to {await svg.SaveAsDataUri()}");

        ValidateDetection(expected, results.Select(r => r.AARectangle).ToList());

    }

    private Rectangle DrawText(Image image, string text, int x, int y)
    {
        image.Mutate(ctx => ctx.DrawText(text, _font, Color.Black, new PointF(x, y)));
        var textRect = TextMeasurer.MeasureAdvance(text, new TextOptions(_font));
        return new Rectangle(x, y, (int)Math.Ceiling(textRect.Width), (int)Math.Ceiling(textRect.Height));
    }

    private void ValidateDetection(List<Rectangle> expected, List<Rectangle> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        var pairs = PairBBoxes(expected, actual);
        foreach (var (iou, expectedIdx, actualIdx) in pairs)
        {
            Assert.True(iou >= 0.5);
        }
    }

    private List<(double IoU, int ExpectedIdx, int ActualIdx)> PairBBoxes(List<Rectangle> expectedBBoxes, List<Rectangle> actualBBoxes)
    {
        Debug.Assert(expectedBBoxes.Count == actualBBoxes.Count);

        List<(double IoU, int ExpectedIdx, int ActualIdx)> pairList = [];

        for (int i = 0; i < expectedBBoxes.Count; i++)
        {
            for (int j = 0; j < actualBBoxes.Count; j++)
            {
                double score = IoU(expectedBBoxes[i], actualBBoxes[j]);
                pairList.Add((score, i, j));
            }
        }

        pairList.Sort();
        pairList.Reverse();

        Dictionary<int, int> pairs = [];

        foreach (var (_, expectedIndex, actualIndex) in pairList)
        {
            if (!pairs.ContainsKey(expectedIndex) && !pairs.ContainsValue(actualIndex))
            {
                pairs[expectedIndex] = actualIndex;
            }
        }

        return pairs
            .Select(p => (IoU(expectedBBoxes[p.Key], actualBBoxes[p.Value]), p.Key, p.Value))
            .ToList();
    }

    private double IoU(Rectangle a, Rectangle b)
    {
        return IoU(ToPoints(a), ToPoints(b));

        static List<(int X, int Y)> ToPoints(Rectangle rect) => [
                (rect.X, rect.Y),
                (rect.X + rect.Width, rect.Y),
                (rect.X + rect.Width, rect.Y + rect.Height),
                (rect.X, rect.Y + rect.Height)
            ];
    }

    private double IoU(List<(int X, int Y)> a, List<(int X, int Y)> b)
    {
        // Convert polygon points to Clipper2 format
        var pathA = new Path64(a.Select(p => new Point64(p.X, p.Y)));
        var pathB = new Path64(b.Select(p => new Point64(p.X, p.Y)));

        // Calculate intersection
        var clipper = new Clipper64();
        clipper.AddSubject(new Paths64 { pathA });
        clipper.AddClip(new Paths64 { pathB });

        var intersection = new Paths64();
        clipper.Execute(ClipType.Intersection, FillRule.NonZero, intersection);

        // Calculate union
        clipper.Clear();
        clipper.AddSubject(new Paths64 { pathA });
        clipper.AddClip(new Paths64 { pathB });

        var union = new Paths64();
        clipper.Execute(ClipType.Union, FillRule.NonZero, union);

        // Calculate areas
        double intersectionArea = intersection.Sum(path => Math.Abs(Clipper.Area(path)));
        double unionArea = union.Sum(path => Math.Abs(Clipper.Area(path)));

        return unionArea > 0 ? intersectionArea / unionArea : 0.0;
    }

    public void Dispose()
    {
        _modelProvider.Dispose();
        GC.SuppressFinalize(this);
    }
}
