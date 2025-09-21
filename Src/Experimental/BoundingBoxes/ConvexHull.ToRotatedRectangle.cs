// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Experimental.BoundingBoxes;

public static partial class RotatedRectangleExtensions
{
    public static RotatedRectangle ToRotatedRectangle(this ConvexHull convexHull)
    {
        if (convexHull.Points.Count < 3)
            throw new ArgumentException("Convex hull must have at least 3 points");

        double minArea = double.MaxValue;
        RotatedRectangle? bestRectangle = null;

        var points = convexHull.Points;

        int n = points.Count;

        // Try each edge as a potential side of the rectangle
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            var edge = (points[j].X - points[i].X, points[j].Y - points[i].Y);

            // Skip zero-length edges
            if (edge.Item1 == 0 && edge.Item2 == 0)
                continue;

            // Find the rectangle aligned with this edge
            var rectangle = FindRectangleAlignedWithEdge(points, edge);
            var area = rectangle.Height * rectangle.Width;

            if (area < minArea)
            {
                minArea = area;
                bestRectangle = rectangle;
            }
        }

        return bestRectangle ?? throw new InvalidOperationException("Could not compute oriented rectangle");
    }

    private static RotatedRectangle FindRectangleAlignedWithEdge(List<Point> points, (int X, int Y) edge)
    {
        // 1. Compute edge unit vector and normal vector. These are the basis vectors for the rectangle
        // 2. Project points onto the basis vectors
        // 3. Find the minimum and maximum projections of points onto the basis vectors
        // 3. Transform from basic vector coordinates back to world coordinates

        var edgeLength = Math.Sqrt(edge.X * edge.X + edge.Y * edge.Y);
        var (ux, uy) = (edge.X / edgeLength, edge.Y / edgeLength);  // Edge unit vector
        var (nx, ny) = (-uy, ux);  // Edge normal vector

        // Compute minimum and maximum projections of points onto the basis vectors
        var minU = double.PositiveInfinity;
        var maxU = double.NegativeInfinity;
        var minN = double.PositiveInfinity;
        var maxN = double.NegativeInfinity;
        foreach (var point in points)
        {
            var projU = point.X * ux + point.Y * uy;
            var projN = point.X * nx + point.Y * ny;

            minU = Math.Min(minU, projU);
            maxU = Math.Max(maxU, projU);
            minN = Math.Min(minN, projN);
            maxN = Math.Max(maxN, projN);
        }

        var corner0 = (minU * ux + maxN * nx, minU * uy + maxN * ny);  // (minU, maxN)
        var corner1 = (maxU * ux + maxN * nx, maxU * uy + maxN * ny);  // (maxU, maxN)
        var corner2 = (maxU * ux + minN * nx, maxU * uy + minN * ny);  // (maxU, minN)
        var corner3 = (minU * ux + minN * nx, minU * uy + minN * ny);  // (minU, minN)

        List<PointF> corners = [corner0, corner1, corner2, corner3];

        var rect = corners.ToRotatedRectangle();
        if (Math.Abs(rect.Angle - Math.PI / 2) < 0.00001)
        {

        }

        return rect;
    }
}
