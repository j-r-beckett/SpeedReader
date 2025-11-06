// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Immutable;

namespace Ocr.Geometry;

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
}
