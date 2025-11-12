// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json.Serialization;
using Clipper2Lib;

namespace Ocr.Geometry;

public record Polygon
{
    [JsonPropertyName("points")]
    public IReadOnlyList<PointF> Points { get; }

    public Polygon() => Points = [];

    public Polygon(List<Point> points) => Points = points.Select(p => (PointF)p).ToList().AsReadOnly();

    public Polygon(List<PointF> points) => Points = points.AsReadOnly();

    public Polygon? Dilate(double dilationRatio)
    {
        var clipperPathD = new PathD();
        foreach (var point in Points)
        {
            clipperPathD.Add(new PointD(point.X, point.Y));
        }

        double area = Math.Abs(Clipper.Area(clipperPathD));
        double perimeter = CalculatePerimeter(clipperPathD);

        double offset = area * dilationRatio / perimeter;

        var clipperPath = Clipper.Path64(clipperPathD);
        var clipperOffset = new ClipperOffset();
        clipperOffset.AddPath(clipperPath, JoinType.Round, EndType.Polygon);

        var solution = new Paths64();
        clipperOffset.Execute(offset, solution);

        if (solution.Count == 0)
            return null;

        var dilatedPolygon = new List<PointF>(solution[0].Count);
        for (int i = 0; i < solution[0].Count; i++)
        {
            var point = solution[0][i];
            dilatedPolygon.Add((point.X, point.Y));
        }

        return new Polygon(dilatedPolygon);

        static double CalculatePerimeter(PathD path)
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

    public Polygon Clamp(int height, int width)
    {
        return new Polygon(Points.Select(ClampPoint).ToList());

        PointF ClampPoint(PointF p) => new()
        {
            X = Math.Clamp(p.X, 0, width),
            Y = Math.Clamp(p.Y, 0, height)
        };
    }

    public Polygon Scale(double scale)
    {
        return new Polygon(Points.Select(ScalePoint).ToList());

        PointF ScalePoint(PointF p) => new()
        {
            X = p.X * scale,
            Y = p.Y * scale
        };
    }

    public Polygon Simplify(double tolerance = 1)
    {
        return new Polygon(SimplifyInternal(Points.ToList(), tolerance));

        static List<PointF> SimplifyInternal(List<PointF> polygon, double tolerance)
        {
            // Douglas-Peucker polygon simplification

            if (polygon.Count <= 3)
                return polygon;

            // Find the point with maximum distance from the line segment
            var start = polygon[0];
            var end = polygon[^1];

            double maxDistance = 0;
            int maxIndex = 0;

            for (int i = 1; i < polygon.Count - 1; i++)
            {
                double distance = PerpendicularDistance(polygon[i], start, end);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    maxIndex = i;
                }
            }

            // If the maximum distance is greater than tolerance, recursively simplify
            if (maxDistance > tolerance)
            {
                var leftSegment = SimplifyInternal(polygon[..(maxIndex + 1)], tolerance);
                var rightSegment = SimplifyInternal(polygon[maxIndex..], tolerance);
                return leftSegment.Concat(rightSegment.Skip(1)).ToList(); // Skip duplicate point at junction
            }

            // All points between start and end are within tolerance, keep only endpoints
            return [start, end];

            static double PerpendicularDistance(PointF point, PointF lineStart, PointF lineEnd)
            {
                double dx = lineEnd.X - lineStart.X;
                double dy = lineEnd.Y - lineStart.Y;

                // If the line segment has zero length, return distance to start point
                if (dx == 0 && dy == 0)
                    return Math.Sqrt(Math.Pow(point.X - lineStart.X, 2) + Math.Pow(point.Y - lineStart.Y, 2));

                // Calculate perpendicular distance using the formula:
                // |ax + by + c| / sqrt(a^2 + b^2)
                // where the line equation is ax + by + c = 0
                double a = dy;
                double b = -dx;
                double c = dx * lineStart.Y - dy * lineStart.X;

                return Math.Abs(a * point.X + b * point.Y + c) / Math.Sqrt(a * a + b * b);
            }
        }
    }
}
