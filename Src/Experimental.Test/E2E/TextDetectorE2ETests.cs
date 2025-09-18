// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Clipper2Lib;
using Core;
using Experimental.Inference;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Resources;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TestUtils;
using Xunit.Abstractions;

namespace Experimental.Test.E2E;

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
    public async Task ReturnsCorrectResults_StraightText()
    {
        using var image = new Image<Rgb24>(720, 640, Color.White);

        List<List<(double X, double Y)>> expected =
        [
            DrawText(image, "undertake", 100, 400),
            DrawText(image, "yup", 300, 150),
            DrawText(image, "anabranch", 500, 100),
            DrawText(image, "happy", 600, 200),
        ];

        await TestTextDetection(image, expected);
    }

    [Fact]
    public async Task ReturnsCorrectResults_RotatedText()
    {
        using var image = new Image<Rgb24>(720, 640, Color.White);

        List<List<(double X, double Y)>> expected =
        [
            DrawText(image, "dastardly", 450, 450, 35),
            DrawText(image, "citizen", 300, 150, 15),
            DrawText(image, "nowhere", 600, 100, 50),
            DrawText(image, "guppy", 150, 500, -45),
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
        var detector = new TextDetector(dbnetRunner, vizBuilder);

        var results = await detector.Detect(image);

        var expectedAxisAligned = expectedBBoxes.Select(ToAxisAlignedRectangle).ToList();

        var svg = vizBuilder
            .AddExpectedAxisAlignedBBoxes(expectedAxisAligned)
            .AddExpectedOrientedBBoxes(expectedBBoxes, true)
            .RenderSvg();

        _logger.LogInformation($"Saved visualization to {await svg.SaveAsDataUri()}");

        ValidateOrientedBBoxes(expectedBBoxes, results.Select(r => r.ORectangle).ToList());
        ValidateAxisAlignedBBoxes(expectedAxisAligned, results.Select(r => r.AARectangle).ToList());

        foreach (var result in results)
        {
            Assert.True(result.Polygon.Count >= 4);
        }
    }

    private List<(double X, double Y)> DrawText(Image image, string text, int x, int y, float angleDegrees = 0)
    {
        var textRect = TextMeasurer.MeasureAdvance(text, new TextOptions(_font));
        var width = Math.Ceiling(textRect.Width);
        var height = Math.Ceiling(textRect.Height);

        // Draw text, rotated angleDegrees counterclockwise around (x, y)
        image.Mutate(ctx => ctx
            .SetDrawingTransform(Matrix3x2Extensions.CreateRotationDegrees(-angleDegrees, new PointF(x, y)))
            .DrawText(text, _font, Color.Black, new PointF(x, y)));

        // Calculate oriented rectangle; start with 4 corners outlining a rectangle at the origin
        List<(double X, double Y)> corners =
        [
            (0, 0),           // Top-left
            (width, 0),       // Top-right
            (width, height),  // Bottom-right
            (0, height)       // Bottom-left
        ];

        // Rotate the rectangle angleDegrees around the origin, then translate to (x, y)
        var angleRadians = -angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(angleRadians);
        var sin = Math.Sin(angleRadians);

        return corners.Select(corner =>
        {
            var rotatedX = corner.X * cos - corner.Y * sin + x;
            var rotatedY = corner.X * sin + corner.Y * cos + y;
            return (rotatedX, rotatedY);
        }).ToList();
    }

    private Rectangle ToAxisAlignedRectangle(List<(double X, double Y)> orientedRect)
    {
        if (orientedRect == null || orientedRect.Count == 0)
        {
            return Rectangle.Empty;
        }

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var point in orientedRect)
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }

        return new Rectangle(
            (int)Math.Floor(minX),
            (int)Math.Floor(minY),
            (int)Math.Ceiling(maxX - minX),
            (int)Math.Ceiling(maxY - minY)
        );
    }

    private void ValidateAxisAlignedBBoxes(List<Rectangle> expected, List<Rectangle> actual)
        => ValidateOrientedBBoxes(expected.Select(ToPoints).ToList(), actual.Select(ToPoints).ToList());

    private void ValidateOrientedBBoxes(List<List<(double X, double Y)>> expected, List<List<(double X, double Y)>> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        var pairs = PairBBoxes(expected, actual);
        foreach (var iou in pairs)
        {
            Assert.True(iou >= 0.5);
        }
    }

    private List<double> PairBBoxes(
        List<List<(double X, double Y)>> expectedBBoxes, List<List<(double X, double Y)>> actualBBoxes)
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
            .Select(p => IoU(expectedBBoxes[p.Key], actualBBoxes[p.Value]))
            .ToList();
    }

    private static List<(double X, double Y)> ToPoints(Rectangle rect) => [
        (rect.X, rect.Y),
        (rect.X + rect.Width, rect.Y),
        (rect.X + rect.Width, rect.Y + rect.Height),
        (rect.X, rect.Y + rect.Height)
    ];

    private double IoU(List<(double X, double Y)> a, List<(double X, double Y)> b)
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
