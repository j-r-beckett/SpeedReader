// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Clipper2Lib;

namespace Ocr.Geometry;

public static partial class PolygonExtensions
{
    private const double MinimumArea = 9;

    public static Polygon Dilate(this Polygon polygon, double dilationRatio)
    {
        var points = polygon.Points;

        if (points.Count < 3)
        {
            return new Polygon(Array.Empty<Point>());
        }

        var clipperPathD = new PathD();
        foreach (var point in points)
        {
            clipperPathD.Add(new PointD(point.X, point.Y));
        }

        double area = Math.Abs(Clipper.Area(clipperPathD));
        double perimeter = CalculatePerimeter(clipperPathD);

        if (perimeter <= 0 || area < MinimumArea)
        {
            return new Polygon(Array.Empty<Point>());
        }

        double offset = area * dilationRatio / perimeter;

        var clipperPath = Clipper.Path64(clipperPathD);
        var clipperOffset = new ClipperOffset();
        clipperOffset.AddPath(clipperPath, JoinType.Round, EndType.Polygon);

        var solution = new Paths64();
        clipperOffset.Execute(offset, solution);

        if (solution.Count == 0 || solution[0].Count < 3)
        {
            return new Polygon(Array.Empty<Point>());
        }

        var dilatedPolygon = new List<Point>(solution[0].Count);
        for (int i = 0; i < solution[0].Count; i++)
        {
            var point = solution[0][i];
            dilatedPolygon.Add(((int)point.X, (int)point.Y));
        }

        return new Polygon(dilatedPolygon);
    }

    private static double CalculatePerimeter(PathD path)
    {
        double perimeter = 0;
        for (int i = 0; i < path.Count; i++)
        {
            var current = path[i];
            var next = path[(i + 1) % path.Count];
            var dx = next.x - current.x;
            var dy = next.y - current.y;
            perimeter += Math.Sqrt(dx * dx + dy * dy);
        }
        return perimeter;
    }
}
