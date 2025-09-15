// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SixLabors.ImageSharp;

namespace Ocr.Algorithms;

/// <summary>
/// Utilities for computing various bounding shapes around polygon points.
/// </summary>
public static class BoundingRectangles
{
    /// <summary>
    /// Computes the axis-aligned bounding rectangle for a set of polygon points.
    /// Uses the same logic as the original DBNet.GetBoundingBox implementation.
    /// </summary>
    /// <param name="polygon">Polygon points to compute bounding rectangle for</param>
    /// <returns>The smallest axis-aligned rectangle that contains all points</returns>
    /// <exception cref="ArgumentException">Thrown when polygon is null or empty</exception>
    public static Rectangle ComputeAxisAlignedRectangle(List<(int X, int Y)> polygon)
    {
        if (polygon == null || polygon.Count == 0)
            throw new ArgumentException("Polygon cannot be null or empty", nameof(polygon));

        int maxX = int.MinValue;
        int maxY = int.MinValue;
        int minX = int.MaxValue;
        int minY = int.MaxValue;

        foreach ((int x, int y) in polygon)
        {
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
        }

        return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>
    /// Computes the oriented bounding rectangle for a set of polygon points.
    /// Uses naive O(n^4) rotating calipers algorithm - tries all edge pairs as potential sides.
    /// </summary>
    /// <param name="polygon">Convex hull points in counter-clockwise order</param>
    /// <returns>Four corner points of the minimum area oriented rectangle in floating-point precision</returns>
    /// <exception cref="ArgumentException">Thrown when polygon is null or empty</exception>
    public static List<(double X, double Y)> ComputeOrientedRectangle(List<(int X, int Y)> polygon)
    {
        if (polygon == null || polygon.Count == 0)
            throw new ArgumentException("Polygon cannot be null or empty", nameof(polygon));

        if (polygon.Count == 1)
            return new List<(double X, double Y)> { (polygon[0].X, polygon[0].Y), (polygon[0].X, polygon[0].Y), (polygon[0].X, polygon[0].Y), (polygon[0].X, polygon[0].Y) };

        if (polygon.Count == 2)
        {
            var p1 = polygon[0];
            var p2 = polygon[1];
            return new List<(double X, double Y)> { (p1.X, p1.Y), (p2.X, p2.Y), (p2.X, p2.Y), (p1.X, p1.Y) };
        }

        double minArea = double.MaxValue;
        List<(double X, double Y)>? bestRectangle = null;

        int n = polygon.Count;

        // Try each edge as a potential side of the rectangle
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            var edge = (polygon[j].X - polygon[i].X, polygon[j].Y - polygon[i].Y);

            // Skip zero-length edges
            if (edge.Item1 == 0 && edge.Item2 == 0) continue;

            // Find the rectangle aligned with this edge
            var rectangle = FindRectangleAlignedWithEdge(polygon, edge);
            double area = CalculateRectangleArea(rectangle);

            if (area < minArea)
            {
                minArea = area;
                bestRectangle = rectangle;
            }
        }

        return bestRectangle ?? throw new InvalidOperationException("Could not compute oriented rectangle");
    }

    /// <summary>
    /// Finds the minimum bounding rectangle aligned with the given edge direction.
    /// </summary>
    private static List<(double X, double Y)> FindRectangleAlignedWithEdge(List<(int X, int Y)> polygon, (int X, int Y) edge)
    {
        // Normalize the edge to create orthogonal unit vectors
        double edgeLength = Math.Sqrt(edge.X * edge.X + edge.Y * edge.Y);
        double ux = edge.X / edgeLength;  // Unit vector along edge
        double uy = edge.Y / edgeLength;
        double vx = -uy;  // Perpendicular unit vector
        double vy = ux;

        // Project all points onto the two axes
        double minU = double.MaxValue, maxU = double.MinValue;
        double minV = double.MaxValue, maxV = double.MinValue;

        foreach (var point in polygon)
        {
            double projU = point.X * ux + point.Y * uy;
            double projV = point.X * vx + point.Y * vy;

            minU = Math.Min(minU, projU);
            maxU = Math.Max(maxU, projU);
            minV = Math.Min(minV, projV);
            maxV = Math.Max(maxV, projV);
        }

        // Compute corner 0 in floating-point precision
        double corner0X = minU * ux + minV * vx;
        double corner0Y = minU * uy + minV * vy;

        // Compute exact edge vectors from corner 0
        double edgeVector1X = (maxU - minU) * ux; // Vector from corner 0 to corner 1
        double edgeVector1Y = (maxU - minU) * uy;
        double edgeVector2X = (maxV - minV) * vx; // Vector from corner 0 to corner 3
        double edgeVector2Y = (maxV - minV) * vy;

        // Compute other corners using exact floating-point arithmetic
        var corner0 = (corner0X, corner0Y);
        var corner1 = (corner0X + edgeVector1X, corner0Y + edgeVector1Y);
        var corner2 = (corner0X + edgeVector1X + edgeVector2X, corner0Y + edgeVector1Y + edgeVector2Y);
        var corner3 = (corner0X + edgeVector2X, corner0Y + edgeVector2Y);

        return new List<(double X, double Y)> { corner0, corner1, corner2, corner3 };
    }

    /// <summary>
    /// Calculates the area of a rectangle given its four corners.
    /// </summary>
    private static double CalculateRectangleArea(List<(double X, double Y)> rectangle)
    {
        if (rectangle.Count != 4) return 0;

        // Calculate side lengths
        double side1 = Math.Sqrt(Math.Pow(rectangle[1].X - rectangle[0].X, 2) + Math.Pow(rectangle[1].Y - rectangle[0].Y, 2));
        double side2 = Math.Sqrt(Math.Pow(rectangle[2].X - rectangle[1].X, 2) + Math.Pow(rectangle[2].Y - rectangle[1].Y, 2));

        return side1 * side2;
    }
}
