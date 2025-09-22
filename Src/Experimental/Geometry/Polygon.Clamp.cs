// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Experimental.Geometry;

public static partial class PolygonExtensions
{
    public static Polygon Clamp(this Polygon polygon, int height, int width)
    {
        return new Polygon { Points = polygon.Points.Select(ClampPoint).ToList() };

        Point ClampPoint(Point p) => new()
        {
            X = Math.Clamp(p.X, 0, width),
            Y = Math.Clamp(p.Y, 0, height)
        };
    }
}
