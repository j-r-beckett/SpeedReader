// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Experimental.BoundingBoxes;

public static partial class PolygonExtensions
{
    public static Polygon Simplify(this Polygon polygon, double tolerance = 1) =>
        new() { Points = Simplify(polygon.Points, tolerance) };

    private static List<Point> Simplify(this List<Point> polygon, double tolerance)
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
            var leftSegment = Simplify(polygon[..(maxIndex + 1)], tolerance);
            var rightSegment = Simplify(polygon[maxIndex..], tolerance);
            return leftSegment.Concat(rightSegment.Skip(1)).ToList(); // Skip duplicate point at junction
        }

        // All points between start and end are within tolerance, keep only endpoints
        return [start, end];
    }

    private static double PerpendicularDistance(Point point, Point lineStart, Point lineEnd)
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
