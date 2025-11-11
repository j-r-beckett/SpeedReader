// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Ocr.Geometry;

public static partial class PolygonExtensions
{
    public static Polygon Scale(this Polygon polygon, double scale)
    {
        return new Polygon(polygon.Points.Select(ScalePoint));

        Point ScalePoint(Point p) => new()
        {
            X = (int)Math.Round(p.X * scale),
            Y = (int)Math.Round(p.Y * scale)
        };
    }
}
