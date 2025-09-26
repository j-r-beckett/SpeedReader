// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Immutable;

namespace Experimental.Geometry;

public static partial class PolygonExtensions
{
    public static Polygon Scale(this Polygon polygon, double scale)
    {
        return new Polygon { Points = polygon.Points.Select(ScalePoint).ToImmutableList() };

        Point ScalePoint(Point p) => new()
        {
            X = (int)Math.Round(p.X * scale),
            Y = (int)Math.Round(p.Y * scale)
        };
    }

    public static Polygon ScaleX(this Polygon polygon, double scaleX)
    {
        return new Polygon { Points = polygon.Points.Select(ScalePointX).ToImmutableList() };

        Point ScalePointX(Point p) => new()
        {
            X = (int)Math.Round(p.X * scaleX),
            Y = p.Y
        };
    }

    public static Polygon ScaleY(this Polygon polygon, double scaleY)
    {
        return new Polygon { Points = polygon.Points.Select(ScalePointY).ToImmutableList() };

        Point ScalePointY(Point p) => new()
        {
            X = p.X,
            Y = (int)Math.Round(p.Y * scaleY)
        };
    }
}
