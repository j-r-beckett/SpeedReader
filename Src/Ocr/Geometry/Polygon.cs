// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Clipper2Lib;

namespace Ocr.Geometry;

public record Polygon
{
    [JsonPropertyName("points")]
    public required ReadOnlyCollection<Point> Points { get; init; }

    [SetsRequiredMembers]
    public Polygon(IEnumerable<Point> points) => Points = points.ToList().AsReadOnly();

    public ConvexHull ToConvexHull()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Points.Count, 3);

        var points = Points.ToList();  // Copy the points to avoid modifying the original polygon

        var stack = new List<Point>();
        var minYPoint = GetStartPoint(points);
        points.Sort((p1, p2) => ComparePolarAngle(minYPoint, p1, p2));
        stack.Add(points[0]);
        stack.Add(points[1]);

        for (var i = 2; i < points.Count; i++)
        {
            var next = points[i];
            var p = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            while (stack.Count > 0 && CrossProductZ(stack[^1], p, next) <= 0)
            {
                p = stack[^1];
                stack.RemoveAt(stack.Count - 1);
            }
            stack.Add(p);
            stack.Add(next);
        }

        var lastPoint = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        if (CrossProductZ(stack[^1], lastPoint, minYPoint) > 0)
            stack.Add(lastPoint);

        stack.Reverse();

        return new ConvexHull { Points = stack.AsReadOnly() };

        static Point GetStartPoint(List<Point> points)
        {
            var (bestX, bestY) = points[0];

            for (var i = 1; i < points.Count; i++)
            {
                if (points[i].Y < bestY || points[i].Y == bestY && points[i].X < bestX)
                {
                    (bestX, bestY) = points[i];
                }
            }

            return (bestX, bestY);
        }

        static int CrossProductZ(Point a, Point b, Point c) =>
            (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        static int ComparePolarAngle(Point anchor, Point p1, Point p2)
        {
            var crossZ = CrossProductZ(anchor, p1, p2);

            if (crossZ < 0)
                return 1;
            if (crossZ > 0)
                return -1;

            // Points are collinear, break ties by distance
            (int X, int Y) v1 = (p1.X - anchor.X, p1.Y - anchor.Y);
            (int X, int Y) v2 = (p2.X - anchor.X, p2.Y - anchor.Y);
            var dist1 = v1.X * v1.X + v1.Y * v1.Y;
            var dist2 = v2.X * v2.X + v2.Y * v2.Y;
            return dist1.CompareTo(dist2);
        }
    }

    public Polygon Scale(double scale)
    {
        return new Polygon(Points.Select(ScalePoint));

        Point ScalePoint(Point p) => new()
        {
            X = (int)Math.Round(p.X * scale),
            Y = (int)Math.Round(p.Y * scale)
        };
    }

    public Polygon Dilate(double dilationRatio)
    {
        const double MinimumArea = 9;

        var points = Points;

        if (points.Count < 3)
        {
            return new Polygon([]);
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
            return new Polygon([]);
        }

        double offset = area * dilationRatio / perimeter;

        var clipperPath = Clipper.Path64(clipperPathD);
        var clipperOffset = new ClipperOffset();
        clipperOffset.AddPath(clipperPath, JoinType.Round, EndType.Polygon);

        var solution = new Paths64();
        clipperOffset.Execute(offset, solution);

        if (solution.Count == 0 || solution[0].Count < 3)
        {
            return new Polygon([]);
        }

        var dilatedPolygon = new List<Point>(solution[0].Count);
        for (int i = 0; i < solution[0].Count; i++)
        {
            var point = solution[0][i];
            dilatedPolygon.Add(((int)point.X, (int)point.Y));
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
        return new Polygon(Points.Select(ClampPoint));

        Point ClampPoint(Point p) => new()
        {
            X = Math.Clamp(p.X, 0, width),
            Y = Math.Clamp(p.Y, 0, height)
        };
    }

    public Polygon Simplify(double tolerance = 1)
    {
        return new Polygon(SimplifyInternal(Points.ToList(), tolerance));

        static List<Point> SimplifyInternal(List<Point> polygon, double tolerance)
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

            static double PerpendicularDistance(Point point, Point lineStart, Point lineEnd)
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
