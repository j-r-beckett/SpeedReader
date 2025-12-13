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

        if (perimeter == 0)
            return null;

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

    public Polygon Simplify(double aggressiveness = 1)
    {
        // Visvalingam-Whyatt polygon simplification
        // Iteratively removes points that cause the smallest area change

        if (Points.Count <= 3)
            return this;

        // Create a list of vertices with their effective areas
        var vertices = new List<Vertex>();
        for (int i = 0; i < Points.Count; i++)
        {
            vertices.Add(new Vertex
            {
                Point = Points[i],
                Index = i
            });
        }

        // Calculate initial effective areas for all points
        for (int i = 0; i < vertices.Count; i++)
        {
            vertices[i].EffectiveArea = CalculateTriangleArea(
                GetPrevVertex(vertices, i).Point,
                vertices[i].Point,
                GetNextVertex(vertices, i).Point
            );
        }

        // Use a priority queue to efficiently find minimum area point
        var heap = new PriorityQueue<int, double>();
        for (int i = 0; i < vertices.Count; i++)
        {
            heap.Enqueue(i, vertices[i].EffectiveArea);
        }

        // Remove points until we reach tolerance or minimum point count
        while (heap.Count > 0)
        {
            var minIndex = heap.Dequeue();

            // Skip if already removed
            if (vertices[minIndex].Removed)
                continue;

            // Count remaining points
            var remainingCount = vertices.Count(v => !v.Removed);
            if (remainingCount <= 3)
                break;

            // Stop if minimum area exceeds tolerance
            if (vertices[minIndex].EffectiveArea > aggressiveness)
                break;

            // Mark as removed
            vertices[minIndex].Removed = true;

            // Recalculate areas for neighboring vertices
            var prevIdx = GetPrevVertexIndex(vertices, minIndex);
            var nextIdx = GetNextVertexIndex(vertices, minIndex);

            if (prevIdx != -1)
            {
                vertices[prevIdx].EffectiveArea = CalculateTriangleArea(
                    GetPrevVertex(vertices, prevIdx).Point,
                    vertices[prevIdx].Point,
                    GetNextVertex(vertices, prevIdx).Point
                );
                heap.Enqueue(prevIdx, vertices[prevIdx].EffectiveArea);
            }

            if (nextIdx != -1)
            {
                vertices[nextIdx].EffectiveArea = CalculateTriangleArea(
                    GetPrevVertex(vertices, nextIdx).Point,
                    vertices[nextIdx].Point,
                    GetNextVertex(vertices, nextIdx).Point
                );
                heap.Enqueue(nextIdx, vertices[nextIdx].EffectiveArea);
            }
        }

        // Collect remaining points
        var simplified = vertices.Where(v => !v.Removed).Select(v => v.Point).ToList();
        return new Polygon(simplified);

        static double CalculateTriangleArea(PointF p1, PointF p2, PointF p3) =>
            // Use cross product formula: |((p2-p1) × (p3-p1))| / 2
            Math.Abs((p2.X - p1.X) * (p3.Y - p1.Y) - (p3.X - p1.X) * (p2.Y - p1.Y)) / 2;

        static Vertex GetPrevVertex(List<Vertex> vertices, int index)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                if (!vertices[i].Removed)
                    return vertices[i];
            }
            // Wrap around for closed polygon
            for (int i = vertices.Count - 1; i > index; i--)
            {
                if (!vertices[i].Removed)
                    return vertices[i];
            }
            return vertices[index]; // Shouldn't happen if count > 3
        }

        static Vertex GetNextVertex(List<Vertex> vertices, int index)
        {
            for (int i = index + 1; i < vertices.Count; i++)
            {
                if (!vertices[i].Removed)
                    return vertices[i];
            }
            // Wrap around for closed polygon
            for (int i = 0; i < index; i++)
            {
                if (!vertices[i].Removed)
                    return vertices[i];
            }
            return vertices[index]; // Shouldn't happen if count > 3
        }

        static int GetPrevVertexIndex(List<Vertex> vertices, int index)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                if (!vertices[i].Removed)
                    return i;
            }
            // Wrap around for closed polygon
            for (int i = vertices.Count - 1; i > index; i--)
            {
                if (!vertices[i].Removed)
                    return i;
            }
            return -1;
        }

        static int GetNextVertexIndex(List<Vertex> vertices, int index)
        {
            for (int i = index + 1; i < vertices.Count; i++)
            {
                if (!vertices[i].Removed)
                    return i;
            }
            // Wrap around for closed polygon
            for (int i = 0; i < index; i++)
            {
                if (!vertices[i].Removed)
                    return i;
            }
            return -1;
        }
    }

    private class Vertex
    {
        public required PointF Point { get; init; }
        public required int Index { get; init; }
        public double EffectiveArea { get; set; }
        public bool Removed { get; set; }
    }
}
