// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Ocr.Algorithms;

public static class PolygonSimplification
{
    /// <summary>
    /// Simplifies a polygon using the Douglas-Peucker algorithm.
    /// </summary>
    /// <param name="polygon">Array of polygon vertices</param>
    /// <param name="tolerance">Maximum distance tolerance (default: 2.0)</param>
    /// <returns>Simplified polygon as a list of vertices</returns>
    public static List<(int, int)> DouglasPeucker((int X, int Y)[] polygon, double tolerance = 1)
    {
        if (polygon.Length <= 2)
            return polygon.ToList();

        return DouglasPeuckerRecursive(polygon.AsSpan(), tolerance).ToList();
    }

    /// <summary>
    /// Recursive implementation of Douglas-Peucker algorithm.
    /// </summary>
    private static IEnumerable<(int X, int Y)> DouglasPeuckerRecursive(ReadOnlySpan<(int X, int Y)> polygon, double tolerance)
    {
        if (polygon.Length <= 2)
            return polygon.ToArray();

        // Find the point with maximum distance from the line segment
        var start = polygon[0];
        var end = polygon[^1];

        double maxDistance = 0;
        int maxIndex = 0;

        for (int i = 1; i < polygon.Length - 1; i++)
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
            var leftSegment = DouglasPeuckerRecursive(polygon[..(maxIndex + 1)], tolerance);
            var rightSegment = DouglasPeuckerRecursive(polygon[maxIndex..], tolerance);
            return leftSegment.Concat(rightSegment.Skip(1)); // Skip duplicate point at junction
        }
        else
        {
            // All points between start and end are within tolerance, keep only endpoints
            return [start, end];
        }
    }

    /// <summary>
    /// Calculates the perpendicular distance from a point to a line segment.
    /// </summary>
    private static double PerpendicularDistance((int X, int Y) point, (int X, int Y) lineStart, (int X, int Y) lineEnd)
    {
        double dx = lineEnd.X - lineStart.X;
        double dy = lineEnd.Y - lineStart.Y;

        // If the line segment has zero length, return distance to start point
        if (dx == 0 && dy == 0)
            return Math.Sqrt(Math.Pow(point.X - lineStart.X, 2) + Math.Pow(point.Y - lineStart.Y, 2));

        // Calculate perpendicular distance using the formula:
        // |ax + by + c| / sqrt(a² + b²)
        // where the line equation is ax + by + c = 0
        double a = dy;
        double b = -dx;
        double c = dx * lineStart.Y - dy * lineStart.X;

        return Math.Abs(a * point.X + b * point.Y + c) / Math.Sqrt(a * a + b * b);
    }
}
