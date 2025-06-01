namespace TextDetection;

public static class GrahamScan
{
    public static (int X, int Y)[] ComputeConvexHull((int X, int Y)[] points)
    {
        if (points.Length < 3)
        {
            return points.Length < 3 ? [] : points.ToArray();
        }

        var stack = new Stack<(int, int)>();
        var minYPoint = GetStartPoint(points);
        Array.Sort(points, (p1, p2) => ComparePolarAngle(minYPoint, p1, p2));
        stack.Push(points[0]);
        stack.Push(points[1]);

        for (int i = 2; i < points.Length; i++)
        {
            var next = points[i];
            var p = stack.Pop();
            while (stack.Count > 0 && CrossProductZ(stack.Peek(), p, next) <= 0)
            {
                p = stack.Pop();
            }
            stack.Push(p);
            stack.Push(next);
        }

        var lastPoint = stack.Pop();
        if (CrossProductZ(stack.Peek(), lastPoint, minYPoint) > 0)
        {
            stack.Push(lastPoint);
        }

        var result = stack.ToArray();
        Array.Reverse(result);
        return result;
    }

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

    private static int CrossProductZ((int X, int Y) a, (int X, int Y) b, (int X, int Y) c)
    {
        return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    }

    private static int ComparePolarAngle((int X, int Y) anchor, (int X, int Y) p1, (int X, int Y) p2)
    {
        int crossZ = CrossProductZ(anchor, p1, p2);

        if (crossZ < 0) return 1;
        if (crossZ > 0) return -1;

        (int X, int Y) v1 = (p1.X - anchor.X, p1.Y - anchor.Y);
        (int X, int Y) v2 = (p2.X - anchor.X, p2.Y - anchor.Y);
        int dist1 = v1.X * v1.X + v1.Y * v1.Y;
        int dist2 = v2.X * v2.X + v2.Y * v2.Y;
        return dist1.CompareTo(dist2);
    }
}