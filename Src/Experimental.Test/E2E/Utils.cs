// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Clipper2Lib;
using Resources;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

namespace Experimental.Test.E2E;

public static class Utils
{
    public static List<(double X, double Y)> DrawText(Image image, string text, int x, int y, float angleDegrees = 0)
    {
        var font = Fonts.GetFont(fontSize: 24f);
        var textRect = TextMeasurer.MeasureAdvance(text, new TextOptions(font));
        var width = Math.Ceiling(textRect.Width);
        var height = Math.Ceiling(textRect.Height);

        // Draw text, rotated angleDegrees counterclockwise around (x, y)
        image.Mutate(ctx => ctx
            .SetDrawingTransform(Matrix3x2Extensions.CreateRotationDegrees(-angleDegrees, new PointF(x, y)))
            .DrawText(text, font, Color.Black, new PointF(x, y)));

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

    public static Rectangle ToAxisAlignedRectangle(List<(double X, double Y)> orientedRect)
    {
        if (orientedRect.Count == 0)
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

    public static void ValidateAxisAlignedBBoxes(List<Rectangle> expected, List<Rectangle> actual)
        => ValidateOrientedBBoxes(expected.Select(ToPoints).ToList(), actual.Select(ToPoints).ToList());

    public static void ValidateOrientedBBoxes(List<List<(double X, double Y)>> expected, List<List<(double X, double Y)>> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        var pairs = PairBBoxes(expected, actual);
        foreach (var iou in pairs)
        {
            Assert.True(iou >= 0.5);
        }
    }

    public static List<double> PairBBoxes(
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

    public static List<(double X, double Y)> ToPoints(Rectangle rect) => [
        (rect.X, rect.Y),
        (rect.X + rect.Width, rect.Y),
        (rect.X + rect.Width, rect.Y + rect.Height),
        (rect.X, rect.Y + rect.Height)
    ];

    public static double IoU(List<(double X, double Y)> a, List<(double X, double Y)> b)
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
}
