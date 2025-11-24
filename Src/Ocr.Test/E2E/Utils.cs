// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Clipper2Lib;
using Ocr.Geometry;
using Resources.Font;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ocr.Test.E2E;

public static class Utils
{
    public static OcrPipelineResult CreateTestImage(int width, int height, List<(string Text, int X, int Y, int Angle)> texts)
    {
        var image = new Image<Rgb24>(width, height, Color.White);

        // Add thin black border around the edge
        image.Mutate(ctx => ctx.Draw(Color.Black, 1, new RectangleF(0, 0, width - 1, height - 1)));

        List<BoundingBox> detections = [];
        List<(string Text, double Confidence)> recognitions = [];
        foreach (var text in texts)
        {
            var bbox = DrawText(image, text.Text, text.X, text.Y, text.Angle);
            detections.Add(bbox);
            recognitions.Add((text.Text, 1.0));
        }
        return new OcrPipelineResult(image, detections, recognitions, null!);
    }

    public static BoundingBox DrawText(Image image, string text, int x, int y, float angleDegrees = 0)
    {
        var font = EmbeddedFont.Default.Get(fontSize: 24f);
        var textRect = TextMeasurer.MeasureAdvance(text, new TextOptions(font));
        var width = Math.Ceiling(textRect.Width);
        var height = Math.Ceiling(textRect.Height);

        // Draw text, rotated angleDegrees counterclockwise around (x, y)
        image.Mutate(ctx => ctx
            .SetDrawingTransform(Matrix3x2Extensions.CreateRotationDegrees(-angleDegrees, new SixLabors.ImageSharp.PointF(x, y)))
            .DrawText(text, font, Color.Black, new SixLabors.ImageSharp.PointF(x, y)));

        // Create RotatedRectangle
        var angleRadians = -angleDegrees * Math.PI / 180.0;

        var rotatedRect = new RotatedRectangle
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Angle = angleRadians
        };

        return new BoundingBox
        {
            Polygon = rotatedRect.Corners(),
            RotatedRectangle = rotatedRect,
            AxisAlignedRectangle = rotatedRect.ToAxisAlignedRectangle()
        };
    }

    public static void ValidateDetectionsAndRecognitions(OcrPipelineResult expected, OcrPipelineResult actual)
    {
        var expectedBBoxes = expected.Results.Select(r => r.BBox).ToList();
        var actualBBoxes = actual.Results.Select(r => r.BBox).ToList();
        ValidateDetections(expectedBBoxes, actualBBoxes);

        var expectedRects = expectedBBoxes.Select(b => b.RotatedRectangle).ToList();
        var actualRects = actualBBoxes.Select(b => b.RotatedRectangle).ToList();
        var pairs = PairBBoxes(expectedRects, actualRects);

        foreach (var (expectedIdx, actualIdx) in pairs)
        {
            Assert.Equal(expected.Results[expectedIdx].Text, actual.Results[actualIdx].Text);
        }
    }

    public static void ValidateDetections(List<BoundingBox> expected, List<BoundingBox> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        foreach (var polygon in actual.Select(b => b.Polygon))
            Assert.True(polygon.Points.Count >= 3);

        var expectedRotatedRects = expected.Select(b => b.RotatedRectangle).ToList();
        var actualRotatedRects = actual.Select(b => b.RotatedRectangle).ToList();
        ValidateOrientedBBoxes(expectedRotatedRects, actualRotatedRects);

        var expectedAxisAlignedRects = expected.Select(b => b.AxisAlignedRectangle).ToList();
        var actualAxisAlignedRects = actual.Select(b => b.AxisAlignedRectangle).ToList();
        ValidateOrientedBBoxes(expectedAxisAlignedRects.Select(ToRotatedRectangle).ToList(),
            actualAxisAlignedRects.Select(ToRotatedRectangle).ToList());
    }

    public static void ValidateOrientedBBoxes(List<RotatedRectangle> expected, List<RotatedRectangle> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        var pairs = PairBBoxes(expected, actual);
        foreach (var (e, a) in pairs)
        {
            var iou = IoU(expected[e], actual[a]);
            Assert.True(iou >= 0.5);
        }
    }

    public static List<(int, int)> PairBBoxes(
        List<RotatedRectangle> expectedBBoxes, List<RotatedRectangle> actualBBoxes)
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
            .Select(p => (p.Key, p.Value))
            .ToList();
    }


    public static RotatedRectangle ToRotatedRectangle(AxisAlignedRectangle rect) => new()
    {
        X = rect.X,
        Y = rect.Y,
        Width = rect.Width,
        Height = rect.Height,
        Angle = 0
    };

    public static double IoU(RotatedRectangle a, RotatedRectangle b)
    {
        // Convert polygon points to Clipper2 format
        var pathA = new Path64(a.Corners().Points.Select(p => new Point64(p.X, p.Y)));
        var pathB = new Path64(b.Corners().Points.Select(p => new Point64(p.X, p.Y)));

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
}
