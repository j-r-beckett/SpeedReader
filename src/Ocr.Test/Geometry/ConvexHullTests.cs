// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SpeedReader.Ocr.Geometry;

namespace SpeedReader.Ocr.Test.Geometry;

public class ConvexHullTests
{
    private static bool IsCounterClockwise(IReadOnlyList<PointF> points)
    {
        if (points.Count < 3)
            return false;

        // Use the shoelace formula to calculate signed area
        // Negative area = clockwise, Positive area = counter-clockwise
        double sum = 0;
        for (int i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            sum += (next.X - current.X) * (next.Y + current.Y);
        }
        return sum > 0;
    }

    [Fact]
    public void ToConvexHull_Triangle_ReturnsAllThreePoints()
    {
        // Arrange
        var points = new List<Point> { (0, 0), (4, 0), (2, 3) };
        var polygon = new Polygon(points);

        // Act
        var result = polygon.ToConvexHull();

        // Assert
        Assert.Equal(3, result!.Points.Count);
        Assert.Contains((Point)(0, 0), result.Points);
        Assert.Contains((Point)(4, 0), result.Points);
        Assert.Contains((Point)(2, 3), result.Points);
        Assert.True(IsCounterClockwise(result.Points));
    }

    [Fact]
    public void ToConvexHull_Square_ReturnsAllFourCorners()
    {
        // Arrange
        var points = new List<Point> { (0, 0), (4, 0), (4, 4), (0, 4) };
        var polygon = new Polygon(points);

        // Act
        var result = polygon.ToConvexHull();

        // Assert
        Assert.Equal(4, result!.Points.Count);
        Assert.Contains((Point)(0, 0), result.Points);
        Assert.Contains((Point)(4, 0), result.Points);
        Assert.Contains((Point)(4, 4), result.Points);
        Assert.Contains((Point)(0, 4), result.Points);
        Assert.True(IsCounterClockwise(result.Points));
    }

    [Fact]
    public void ToConvexHull_SquareWithInteriorPoints_ReturnsOnlyCorners()
    {
        // Arrange
        var points = new List<Point>
        {
            (0, 0), (4, 0), (4, 4), (0, 4),  // corners
            (2, 2), (1, 1), (3, 3)           // interior points
        };
        var polygon = new Polygon(points);

        // Act
        var result = polygon.ToConvexHull();

        // Assert
        Assert.Equal(4, result!.Points.Count);
        Assert.Contains((Point)(0, 0), result.Points);
        Assert.Contains((Point)(4, 0), result.Points);
        Assert.Contains((Point)(4, 4), result.Points);
        Assert.Contains((Point)(0, 4), result.Points);
    }

    [Fact]
    public void ToConvexHull_CollinearPoints_ReturnsNull()
    {
        // Arrange
        // Collinear points form a degenerate hull (< 3 points after Graham scan)
        var diagonal = new List<Point> { (8, 8), (0, 0), (4, 4), (2, 2), (6, 6) };
        var horizontal = new List<Point> { (7, 3), (0, 3), (2, 3), (1, 3) };
        var vertical = new List<Point> { (3, 9), (3, 1), (3, 5), (3, 3) };

        var diagonalPolygon = new Polygon(diagonal);
        var horizontalPolygon = new Polygon(horizontal);
        var verticalPolygon = new Polygon(vertical);

        // Act
        var diagonalResult = diagonalPolygon.ToConvexHull();
        var horizontalResult = horizontalPolygon.ToConvexHull();
        var verticalResult = verticalPolygon.ToConvexHull();

        // Assert
        Assert.Null(diagonalResult);
        Assert.Null(horizontalResult);
        Assert.Null(verticalResult);
    }

    [Fact]
    public void ToConvexHull_Pentagon_ReturnsAllVertices()
    {
        // Arrange
        // Regular pentagon vertices (approximately)
        var points = new List<Point>
        {
            (3, 0),   // bottom
            (5, 1),   // bottom right
            (4, 3),   // top right
            (2, 3),   // top left
            (1, 1)    // bottom left
        };
        var polygon = new Polygon(points);

        // Act
        var result = polygon.ToConvexHull();

        // Assert
        Assert.Equal(5, result!.Points.Count);
        foreach (var point in points)
        {
            Assert.Contains(point, result.Points);
        }
        Assert.True(IsCounterClockwise(result.Points));
    }

    [Fact]
    public void ToConvexHull_StarShape_ReturnsOuterPoints()
    {
        // Arrange
        var points = new List<Point>
        {
            // Outer points (should be in hull)
            (5, 9), (9, 5), (5, 1), (1, 5),
            // Inner points (should not be in hull)
            (5, 6), (6, 5), (5, 4), (4, 5)
        };
        var polygon = new Polygon(points);

        // Act
        var result = polygon.ToConvexHull();

        // Assert
        Assert.Equal(4, result!.Points.Count);
        Assert.Contains((Point)(5, 9), result.Points);
        Assert.Contains((Point)(9, 5), result.Points);
        Assert.Contains((Point)(5, 1), result.Points);
        Assert.Contains((Point)(1, 5), result.Points);
        Assert.True(IsCounterClockwise(result.Points));
    }

    [Fact]
    public void ToConvexHull_DuplicatePoints_HandlesCorrectly()
    {
        // Arrange
        var points = new List<Point>
        {
            (0, 0), (0, 0), (4, 0), (4, 0), (4, 4), (4, 4), (0, 4), (0, 4)
        };
        var polygon = new Polygon(points);

        // Act
        var result = polygon.ToConvexHull();

        // Assert
        Assert.Equal(4, result!.Points.Count);
        Assert.Contains((Point)(0, 0), result.Points);
        Assert.Contains((Point)(4, 0), result.Points);
        Assert.Contains((Point)(4, 4), result.Points);
        Assert.Contains((Point)(0, 4), result.Points);
    }

    [Fact]
    public void ToConvexHull_RandomPointCloud_ProducesValidHull()
    {
        // Arrange
        // Generate random points inside a circle
        var random = new Random(0);
        var points = new List<PointF>();

        for (int i = 0; i < 500; i++)
        {
            double angle = random.NextDouble() * 2 * Math.PI;
            double radius = random.NextDouble() * 10;
            int x = (int)(radius * Math.Cos(angle)) + 50;
            int y = (int)(radius * Math.Sin(angle)) + 50;
            points.Add((x, y));
        }

        var polygon = new Polygon(points);

        // Act
        var result = polygon.ToConvexHull();

        // Assert
        // Basic validation
        Assert.True(result!.Points.Count >= 3, $"Hull should have at least 3 points, got {result.Points.Count}");
        Assert.True(result.Points.Count <= points.Count, "Hull cannot have more points than input");

        // All hull points should be from original set
        foreach (var hullPoint in result.Points)
        {
            Assert.Contains(hullPoint, points);
        }

        Assert.True(IsCounterClockwise(result.Points));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ToConvexHull_InsufficientPoints_ReturnsNull(int pointCount)
    {
        // Arrange
        var points = new List<Point>();
        for (int i = 0; i < pointCount; i++)
        {
            points.Add((i, i));
        }
        var polygon = new Polygon(points);

        // Act
        var result = polygon.ToConvexHull();

        // Assert
        Assert.Null(result);
    }
}
