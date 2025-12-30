// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json.Serialization;
using Clipper2Lib;

namespace SpeedReader.Ocr.Geometry;

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

    public Polygon Simplify(double epsilon = 1)
    {
        if (Points.Count <= 3)
            return this;

        var path = new PathD(Points.Count);
        foreach (var point in Points)
            path.Add(new PointD(point.X, point.Y));

        PathD simplified = Clipper.SimplifyPath(path, epsilon, false);

        var result = new List<PointF>(simplified.Count);
        foreach (var point in simplified)
            result.Add(((float)point.x, (float)point.y));

        return new Polygon(result);
    }
}
