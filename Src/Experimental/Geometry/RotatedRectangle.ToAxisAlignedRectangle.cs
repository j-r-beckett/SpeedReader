// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Immutable;

namespace Experimental.Geometry;

public static partial class RotatedRectangleExtensions
{
    public static AxisAlignedRectangle ToAxisAlignedRectangle(this RotatedRectangle rotatedRectangle) =>
        rotatedRectangle.Corners().ToAxisAlignedRectangle();

    public static AxisAlignedRectangle ToAxisAlignedRectangle(this ImmutableList<PointF> points) =>
        points.ToList().ToAxisAlignedRectangle();

    public static AxisAlignedRectangle ToAxisAlignedRectangle(this List<Point> points) => points
        .Select(p => (PointF)p)
        .ToList()
        .ToAxisAlignedRectangle();

    public static AxisAlignedRectangle ToAxisAlignedRectangle(this List<PointF> points)
    {
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        foreach (var point in points)
        {
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
        }

        // Ensure rectangle completely encloses points
        return new AxisAlignedRectangle
        {
            X = (int)Math.Floor(minX),
            Y = (int)Math.Floor(minY),
            Width = (int)Math.Ceiling(maxX) - (int)Math.Floor(minX),
            Height = (int)Math.Ceiling(maxY) - (int)Math.Floor(minY)
        };
    }
}
