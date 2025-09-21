// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Clipper2Lib;
using Experimental.BoundingBoxes;
using Resources;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using Point = Experimental.BoundingBoxes.Point;
using PointF = Experimental.BoundingBoxes.PointF;

namespace Experimental.Test.E2E;

public static class Utils
{
    public static RotatedRectangle DrawText(Image image, string text, int x, int y, float angleDegrees = 0)
    {
        var font = Fonts.GetFont(fontSize: 24f);
        var textRect = TextMeasurer.MeasureAdvance(text, new TextOptions(font));
        var width = Math.Ceiling(textRect.Width);
        var height = Math.Ceiling(textRect.Height);

        // Draw text, rotated angleDegrees counterclockwise around (x, y)
        image.Mutate(ctx => ctx
            .SetDrawingTransform(Matrix3x2Extensions.CreateRotationDegrees(-angleDegrees, new SixLabors.ImageSharp.PointF(x, y)))
            .DrawText(text, font, Color.Black, new SixLabors.ImageSharp.PointF(x, y)));

        // Create RotatedRectangle
        var angleRadians = -angleDegrees * Math.PI / 180.0;

        return new RotatedRectangle
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Angle = angleRadians
        };
    }


    public static void ValidateAxisAlignedBBoxes(List<AxisAlignedRectangle> expected, List<AxisAlignedRectangle> actual)
        => ValidateOrientedBBoxes(expected.Select(ToRotatedRectangle).ToList(), actual.Select(ToRotatedRectangle).ToList());

    public static void ValidateOrientedBBoxes(List<RotatedRectangle> expected, List<RotatedRectangle> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        var pairs = PairBBoxes(expected, actual);
        foreach (var iou in pairs)
        {
            Assert.True(iou >= 0.5);
        }
    }

    public static List<double> PairBBoxes(
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
            .Select(p => IoU(expectedBBoxes[p.Key], actualBBoxes[p.Value]))
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
        var cornersA = a.Corners().Select(p => (PointF)p).ToList();
        var cornersB = b.Corners().Select(p => (PointF)p).ToList();
        var pathA = new Path64(cornersA.Select(p => new Point64(p.X, p.Y)));
        var pathB = new Path64(cornersB.Select(p => new Point64(p.X, p.Y)));

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
