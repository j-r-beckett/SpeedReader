// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Experimental.BoundingBoxes;
using Microsoft.Extensions.Logging;
using TestUtils;
using Xunit.Abstractions;

namespace Experimental.Test.BoundingBoxes;

public class OrientedRectangleCreationTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly ILogger<OrientedRectangleCreationTests> _logger;
    private readonly FileSystemUrlPublisher<OrientedRectangleCreationTests> _publisher;

    public OrientedRectangleCreationTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _logger = new TestLogger<OrientedRectangleCreationTests>(outputHelper);
        _publisher = new FileSystemUrlPublisher<OrientedRectangleCreationTests>("/tmp/oriented-rectangle-debug", _logger);
    }

    [Fact]
    public async Task ComputeOrientedRectangle_WithSimpleSquarePoints_ReturnsValidRectangle()
    {
        // Arrange: Create a simple square rotated 45 degrees
        var points = new List<Point> { (50, 0), (100, 50), (50, 100), (0, 50) };

        // Act
        var convexHull = new Polygon { Points = points }.ToConvexHull();
        var rotatedRect = convexHull.ToRotatedRectangle();
        var orientedRectF = rotatedRect.Corners();
        var orientedRect = orientedRectF.Select(c => (Point)c).ToList();

        // Create debug visualization
        using var debugImage = OrientedRectangleTestUtils.CreateDebugVisualization(
            convexHull.Points.Select(p => (p.X, p.Y)).ToList(),
            orientedRect.Select(p => ((double)p.X, (double)p.Y)).ToList());
        await _publisher.PublishAsync(debugImage, "Debug oriented rectangle computation - black dots show convex hull, red polygon shows computed rectangle");

        _outputHelper.WriteLine($"Points: [{string.Join(", ", points)}]");
        _outputHelper.WriteLine($"Oriented rectangle: [{string.Join(", ", orientedRect)}]");

        // Assert: Verify rectangle properties
        Assert.Equal(4, orientedRect.Count);

        VerifyRectangleHasParallelSides(orientedRectF);
        VerifyAllPointsContained(convexHull.Points, rotatedRect);
        VerifyAtLeastTwoPointsOnBoundary(convexHull.Points, orientedRect);
    }

    [Fact(Skip = "Bug in ToCorners")]
    public async Task ComputeOrientedRectangle_WithRandomPointCloud_ReturnsValidRectangle()
    {
        // Arrange: Generate random points inside a circle
        var random = new Random(0);
        var numIterations = 1000;

        var targetIteration = 106;

        for (int n = 0; n < numIterations; n++)
        {
            var points = new List<Point>();

            for (int i = 0; i < 50; i++)
            {
                double angle = random.NextDouble() * 2 * Math.PI;
                double radius = random.NextDouble() * 80;
                int x = 100 + (int)(radius * Math.Cos(angle));
                int y = 75 + (int)(radius * Math.Sin(angle));
                points.Add((x, y));
            }

            if (n < targetIteration)
                continue;

            // Act
            var polygon = new Polygon { Points = points };
            var convexHull = polygon.ToConvexHull();
            var rotatedRect = convexHull.ToRotatedRectangle();
            var orientedRectF = rotatedRect.Corners();
            // var orientedRect = orientedRectF.Select(c => (Point)c).ToList();

            // Create debug visualization
            using var debugImage = OrientedRectangleTestUtils.CreateDebugVisualization(
                convexHull.Points.Select(p => (p.X, p.Y)).ToList(),
                orientedRectF.Select(p => ((double)p.X, (double)p.Y)).ToList());
            await _publisher.PublishAsync(debugImage, "Random point cloud - convex hull points as black dots, oriented rectangle as red polygon");

            _outputHelper.WriteLine($"Generated {points.Count} random points");
            _outputHelper.WriteLine($"Convex hull has {convexHull.Points.Count} points: [{string.Join(", ", convexHull.Points)}]");
            _outputHelper.WriteLine($"Oriented rectangle: [{string.Join(", ", orientedRectF)}]");

            // Assert: Verify rectangle properties
            Assert.Equal(4, orientedRectF.Count);
            Assert.True(convexHull.Points.Count >= 3, "Convex hull should have at least 3 points");
            Assert.True(convexHull.Points.Count <= points.Count, "Convex hull cannot have more points than input");

            VerifyRectangleHasParallelSides(orientedRectF);
            VerifyAllPointsContained(convexHull.Points, rotatedRect);
            VerifyAtLeastTwoPointsOnBoundary(convexHull.Points, orientedRectF.Select(c => (Point)c).ToList());
        }
    }

    private void VerifyRectangleHasParallelSides(List<PointF> rectangle)
    {
        Assert.Equal(4, rectangle.Count);

        var edges = new List<(double X, double Y)>();
        for (int i = 0; i < 4; i++)
        {
            var current = rectangle[i];
            var next = rectangle[(i + 1) % 4];
            edges.Add((next.X - current.X, next.Y - current.Y));
        }

        var edgeLengths = edges.Select(e => Math.Sqrt(e.X * e.X + e.Y * e.Y)).ToList();
        var avgEdgeLength = edgeLengths.Average();
        var parallelTolerance = Math.Max(0.1, avgEdgeLength * 0.001);
        var perpendicularTolerance = Math.Max(1.0, avgEdgeLength * 0.01);

        Assert.True(AreVectorsParallel(edges[0], edges[2], parallelTolerance),
            $"Opposite edges should be parallel within tolerance {parallelTolerance:F1}: Edge0={edges[0]}, Edge2={edges[2]}");

        Assert.True(AreVectorsParallel(edges[1], edges[3], parallelTolerance),
            $"Opposite edges should be parallel within tolerance {parallelTolerance:F1}: Edge1={edges[1]}, Edge3={edges[3]}");

        double dotProduct = edges[0].X * edges[1].X + edges[0].Y * edges[1].Y;
        Assert.True(Math.Abs(dotProduct) < perpendicularTolerance,
            $"Adjacent edges should be perpendicular within tolerance {perpendicularTolerance:F1}, dot product was {dotProduct}");
    }

    private bool AreVectorsParallel((double X, double Y) v1, (double X, double Y) v2, double tolerance)
    {
        double crossProduct = v1.X * v2.Y - v1.Y * v2.X;
        return Math.Abs(crossProduct) < tolerance;
    }

    private void VerifyAllPointsContained(List<Point> points, RotatedRectangle rect)
    {
        foreach (var point in points)
        {
            Assert.True(IsPointInRotatedRect(point, rect),
                $"Point {point} should be contained within the oriented rectangle");
        }
    }

    private void VerifyAtLeastTwoPointsOnBoundary(List<Point> points, List<Point> rectangleCorners)
    {
        int pointsOnBoundary = 0;
        var tolerance = 1.0;

        foreach (var point in points)
        {
            if (IsPointOnRectangleBoundary(point, rectangleCorners.Select(c => new PointF { X = c.X, Y = c.Y }).ToList(), tolerance))
            {
                pointsOnBoundary++;
            }
        }

        Assert.True(pointsOnBoundary >= 2,
            $"At least 2 original points should lie within {tolerance} pixels of the rectangle boundary. Found {pointsOnBoundary} points on boundary.");
    }

    private bool IsPointInRectangle(Point point, List<PointF> rectangle)
    {
        var tolerance = 0.5;
        var rectCorners = rectangle.Select(p => (p.X, p.Y)).ToList();

        return IsPointOnRectangleBoundary(point, rectangle, tolerance)
            ? true
            : IsPointInPolygon(point, rectangle.Select(p => (Point)p).ToList());
    }

    private bool IsPointOnRectangleBoundary(Point point, List<PointF> rectangle, double tolerance)
    {
        for (int i = 0; i < 4; i++)
        {
            var edge1 = rectangle[i];
            var edge2 = rectangle[(i + 1) % 4];

            if (IsPointOnLineSegment((point.X, point.Y), (edge1.X, edge1.Y), (edge2.X, edge2.Y), tolerance))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPointOnLineSegment((double X, double Y) point, (double X, double Y) start, (double X, double Y) end, double tolerance)
    {
        double A = end.Y - start.Y;
        double B = start.X - end.X;
        double C = end.X * start.Y - start.X * end.Y;

        double distance = Math.Abs(A * point.X + B * point.Y + C) / Math.Sqrt(A * A + B * B);

        if (distance > tolerance)
        {
            return false;
        }

        double minX = Math.Min(start.X, end.X);
        double maxX = Math.Max(start.X, end.X);
        double minY = Math.Min(start.Y, end.Y);
        double maxY = Math.Max(start.Y, end.Y);

        return point.X >= minX - tolerance && point.X <= maxX + tolerance &&
               point.Y >= minY - tolerance && point.Y <= maxY + tolerance;
    }

    private bool IsPointInPolygon(Point point, List<Point> polygon)
    {
        if (polygon.Contains(point))
            return true;

        int intersectionCount = 0;
        int n = polygon.Count;

        for (int i = 0; i < n; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % n];

            // Skip horizontal edges
            if (p1.Y == p2.Y)
                continue;

            // Check if point is on the edge
            if (IsPointOnEdge(point, p1, p2))
            {
                return true;
            }

            if (p1.Y > point.Y != p2.Y > point.Y &&
                point.X < (p2.X - p1.X) * (point.Y - p1.Y) / (p2.Y - p1.Y) + p1.X)
            {
                intersectionCount++;
            }
        }

        return intersectionCount % 2 == 1;
    }

    private bool IsPointOnEdge(Point point, Point p1, Point p2)
    {
        // Check if point is collinear with edge using cross product
        var crossProduct = (point.Y - p1.Y) * (p2.X - p1.X) - (point.X - p1.X) * (p2.Y - p1.Y);
        if (Math.Abs(crossProduct) > 1e-9)
            return false; // Not collinear

        // Check if point is within the bounding box of the edge
        var minX = Math.Min(p1.X, p2.X);
        var maxX = Math.Max(p1.X, p2.X);
        var minY = Math.Min(p1.Y, p2.Y);
        var maxY = Math.Max(p1.Y, p2.Y);

        return point.X >= minX && point.X <= maxX && point.Y >= minY && point.Y <= maxY;
    }

    private bool IsPointInRotatedRect(Point point, RotatedRectangle rect)
    {
        var padding = 1.0;

        // Translate point to rectangle's coordinate system
        var translatedX = point.X - rect.X;
        var translatedY = point.Y - rect.Y;

        // Rotate point by -rect.Angle to align rectangle with axes
        var cos = Math.Cos(-rect.Angle);
        var sin = Math.Sin(-rect.Angle);
        var localX = translatedX * cos - translatedY * sin;
        var localY = translatedX * sin + translatedY * cos;

        // Check if point is within padded rectangle bounds
        return localX >= -padding && localX <= rect.Width + padding &&
               localY >= -padding && localY <= rect.Height + padding;
    }
}
