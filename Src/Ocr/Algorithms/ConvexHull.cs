// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Ocr.Algorithms;

public static class ConvexHull
{
    /// <summary>
    /// Computes the convex hull of a set of 2D points using the Graham scan algorithm.
    /// Returns the vertices of the convex hull in counter-clockwise order.
    /// </summary>
    /// <param name="points">Array of points to compute the convex hull for</param>
    /// <returns>
    /// Array of points representing the convex hull vertices in counter-clockwise order.
    /// Returns empty array if fewer than 3 points provided.
    /// </returns>
    /// <seealso href="https://www.youtube.com/watch?v=B2AJoQSZf4M">Goated Graham Scan explanation</seealso>
    public static List<(int X, int Y)> GrahamScan((int X, int Y)[] points)
    {
        if (points.Length < 3)
        {
            return [];
        }

        var stack = new List<(int, int)>();
        var minYPoint = GetStartPoint(points);
        Array.Sort(points, (p1, p2) => ComparePolarAngle(minYPoint, p1, p2));
        stack.Add(points[0]);
        stack.Add(points[1]);

        for (int i = 2; i < points.Length; i++)
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
        {
            stack.Add(lastPoint);
        }

        stack.Reverse();
        return stack;
    }

    /// <summary>
    /// Finds the starting point for the Graham scan algorithm.
    /// Selects the point with the lowest Y coordinate, breaking ties by choosing the leftmost point.
    /// </summary>
    /// <param name="points">Array of points to search</param>
    /// <returns>The point with minimum Y coordinate (and minimum X if tied)</returns>
    private static (int X, int Y) GetStartPoint((int X, int Y)[] points)
    {
        (int bestX, int bestY) = points[0];

        for (int i = 1; i < points.Length; i++)
        {
            if (points[i].Y < bestY || points[i].Y == bestY && points[i].X < bestX)
            {
                (bestX, bestY) = points[i];
            }
        }

        return (bestX, bestY);
    }

    /// <summary>
    /// Calculates the Z component of the cross product of vectors AB and AC.
    /// Used to determine the orientation of three points in 2D space.
    /// </summary>
    /// <param name="a">The anchor point (start of both vectors)</param>
    /// <param name="b">The end point of the first vector AB</param>
    /// <param name="c">The end point of the second vector AC</param>
    /// <returns>
    /// Positive value if points form a counter-clockwise turn,
    /// negative value if clockwise turn,
    /// zero if points are collinear
    /// </returns>
    private static int CrossProductZ((int X, int Y) a, (int X, int Y) b, (int X, int Y) c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    /// <summary>
    /// Compares two points by their polar angle relative to an anchor point.
    /// Used for sorting points in counter-clockwise order around the anchor.
    /// If angles are equal, sorts by distance from anchor (closer first).
    /// </summary>
    /// <param name="anchor">The reference point for polar angle calculation</param>
    /// <param name="p1">First point to compare</param>
    /// <param name="p2">Second point to compare</param>
    /// <returns>
    /// Negative value if p1 should come before p2 in sorted order,
    /// positive value if p1 should come after p2,
    /// zero if points have equal polar angle and distance
    /// </returns>
    private static int ComparePolarAngle((int X, int Y) anchor, (int X, int Y) p1, (int X, int Y) p2)
    {
        int crossZ = CrossProductZ(anchor, p1, p2);

        if (crossZ < 0)
        {
            return 1;
        }

        if (crossZ > 0)
        {
            return -1;
        }

        (int X, int Y) v1 = (p1.X - anchor.X, p1.Y - anchor.Y);
        (int X, int Y) v2 = (p2.X - anchor.X, p2.Y - anchor.Y);
        int dist1 = v1.X * v1.X + v1.Y * v1.Y;
        int dist2 = v2.X * v2.X + v2.Y * v2.Y;
        return dist1.CompareTo(dist2);
    }
}
