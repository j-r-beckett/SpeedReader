// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Ocr.Geometry;

namespace Ocr.Test.BoundingBoxes;

public class ConvexHullTests
{
    [Theory]
    [InlineData(new int[0], new int[0])]  // Empty array
    [InlineData(new[] { 5 }, new[] { 3 })]  // Single point
    [InlineData(new[] { 0, 3 }, new[] { 0, 4 })]  // Two points
    public void ConvexHull_FewerThanThreePoints_ThrowsArgumentOutOfRangeException(int[] xCoords, int[] yCoords)
    {
        // Arrange
        var points = xCoords.Zip(yCoords, (x, y) => (Point)(x, y)).ToList();
        var polygon = new Polygon(points);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => polygon.ToConvexHull());
    }

    [Fact]
    public void ConvexHull_Triangle_ReturnsAllThreePoints()
    {
        // Arrange
        var points = new List<Point> { (0, 0), (4, 0), (2, 3) };
        var polygon = new Polygon(points);

        // Act
        var result = polygon.ToConvexHull();

        // Assert
        Assert.Equal(3, result.Points.Count);
        Assert.Contains((Point)(0, 0), result.Points);
        Assert.Contains((Point)(4, 0), result.Points);
        Assert.Contains((Point)(2, 3), result.Points);
    }

    [Fact]
    public void ConvexHull_Square_ReturnsAllFourCorners()
    {
        // Arrange
        var points = new List<Point> { (0, 0), (4, 0), (4, 4), (0, 4) };
        var polygon = new Polygon(points);

        // Act
        var result = polygon.ToConvexHull();

        // Assert
        Assert.Equal(4, result.Points.Count);
        Assert.Contains((Point)(0, 0), result.Points);
        Assert.Contains((Point)(4, 0), result.Points);
        Assert.Contains((Point)(4, 4), result.Points);
        Assert.Contains((Point)(0, 4), result.Points);
    }

    [Fact]
    public void ConvexHull_SquareWithInteriorPoints_ReturnsOnlyCorners()
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
        Assert.Equal(4, result.Points.Count);
        Assert.Contains((Point)(0, 0), result.Points);
        Assert.Contains((Point)(4, 0), result.Points);
        Assert.Contains((Point)(4, 4), result.Points);
        Assert.Contains((Point)(0, 4), result.Points);
    }

    [Fact]
    public void ConvexHull_CollinearPoints_ReturnsMinimalSet()
    {
        // Arrange
        // All collinear cases should return minimal point set (strict convex hull)
        // For us, that means the point with the smallest (y, x)
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
        Assert.Single(diagonalResult.Points);
        Assert.Contains((Point)(0, 0), diagonalResult.Points); // Start point (lowest Y)

        Assert.Single(horizontalResult.Points);
        Assert.Contains((Point)(0, 3), horizontalResult.Points); // Start point (lowest Y, leftmost X)

        Assert.Single(verticalResult.Points);
        Assert.Contains((Point)(3, 1), verticalResult.Points); // Start point (lowest Y)
    }

    [Fact]
    public void ConvexHull_Pentagon_ReturnsAllVertices()
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
        Assert.Equal(5, result.Points.Count);
        foreach (var point in points)
        {
            Assert.Contains(point, result.Points);
        }
    }

    [Fact]
    public void ConvexHull_StarShape_ReturnsOuterPoints()
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
        Assert.Equal(4, result.Points.Count);
        Assert.Contains((Point)(5, 9), result.Points);
        Assert.Contains((Point)(9, 5), result.Points);
        Assert.Contains((Point)(5, 1), result.Points);
        Assert.Contains((Point)(1, 5), result.Points);
    }

    [Fact]
    public void ConvexHull_DuplicatePoints_HandlesCorrectly()
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
        Assert.Equal(4, result.Points.Count);
        Assert.Contains((Point)(0, 0), result.Points);
        Assert.Contains((Point)(4, 0), result.Points);
        Assert.Contains((Point)(4, 4), result.Points);
        Assert.Contains((Point)(0, 4), result.Points);
    }

    [Fact]
    public void ConvexHull_NonNegativeCoordinates_WorksCorrectly()
    {
        // Arrange
        var points = new List<Point> { (0, 4), (2, 2), (0, 0), (4, 4), (4, 0) };
        var polygon = new Polygon(points);

        // Act
        var result = polygon.ToConvexHull();

        // Assert
        Assert.Equal(4, result.Points.Count);
        Assert.Contains((Point)(0, 0), result.Points);
        Assert.Contains((Point)(4, 0), result.Points);
        Assert.Contains((Point)(4, 4), result.Points);
        Assert.Contains((Point)(0, 4), result.Points);
    }

    [Fact]
    public void ConvexHull_LargeCoordinates_WorksCorrectly()
    {
        // Arrange
        var points = new List<Point>
        {
            (1000, 1000), (2000, 1000), (2000, 2000), (1000, 2000), (1500, 1500)
        };
        var polygon = new Polygon(points);

        // Act
        var result = polygon.ToConvexHull();

        // Assert
        Assert.Equal(4, result.Points.Count);
        Assert.Contains((Point)(1000, 1000), result.Points);
        Assert.Contains((Point)(2000, 1000), result.Points);
        Assert.Contains((Point)(2000, 2000), result.Points);
        Assert.Contains((Point)(1000, 2000), result.Points);
    }

    [Fact]
    public void ConvexHull_RandomPointCloud_ProducesValidHull()
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
        Assert.True(result.Points.Count >= 3, $"Hull should have at least 3 points, got {result.Points.Count}");
        Assert.True(result.Points.Count <= points.Count, "Hull cannot have more points than input");

        // All hull points should be from original set
        foreach (var hullPoint in result.Points)
        {
            Assert.Contains(hullPoint, points);
        }
    }
}
