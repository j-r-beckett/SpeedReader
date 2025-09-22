// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Immutable;

namespace Experimental.Geometry;

public static partial class PolygonExtensions
{
    public static ConvexHull ToConvexHull(this Polygon polygon)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(polygon.Points.Count, 3);

        var points = polygon.Points.ToList();  // Copy the points to avoid modifying the original polygon

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

        return new ConvexHull { Points = stack.ToImmutableList() };
    }

    private static Point GetStartPoint(List<Point> points)
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

    private static int CrossProductZ(Point a, Point b, Point c) =>
        (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static int ComparePolarAngle(Point anchor, Point p1, Point p2)
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
