// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Ocr.Geometry;

namespace Ocr.Test;

public class PolygonDilationTests
{
    private const double DefaultDilationRatio = 1.5;

    [Fact]
    public void Dilate_EmptyPolygon_ReturnsNull()
    {
        var polygon = new Polygon();

        var result = polygon.Dilate(DefaultDilationRatio);

        Assert.Null(result);
    }

    [Fact]
    public void Dilate_SinglePoint_ReturnsNull()
    {
        var polygon = new Polygon([(5, 3)]);

        var result = polygon.Dilate(DefaultDilationRatio);

        Assert.Null(result);
    }

    [Fact]
    public void Dilate_ZeroPerimeter_ReturnsNull()
    {
        // All points are the same (zero perimeter)
        var polygon = new Polygon([(5, 5), (5, 5), (5, 5)]);

        var result = polygon.Dilate(DefaultDilationRatio);

        Assert.Null(result);
    }

    [Fact]
    public void Dilate_MinimumValidTriangle_ReturnsDilatedPolygon()
    {
        // Triangle with area >= 9 pixels: area = 0.5 * 6 * 3 = 9
        var polygon = new Polygon([(0, 0), (6, 0), (3, 3)]);

        var result = polygon.Dilate(DefaultDilationRatio);

        Assert.NotEmpty(result!.Points);
        Assert.True(result.Points.Count >= 3);

        // Dilated polygon should be larger
        var originalBounds = GetBounds(polygon);
        var dilatedBounds = GetBounds(result);

        Assert.True(dilatedBounds.Width >= originalBounds.Width);
        Assert.True(dilatedBounds.Height >= originalBounds.Height);
    }

    [Fact]
    public void Dilate_Square_ExpandsPolygon()
    {
        // 10x10 square (area = 100)
        var polygon = new Polygon([(0, 0), (10, 0), (10, 10), (0, 10)]);

        var result = polygon.Dilate(DefaultDilationRatio);

        Assert.NotEmpty(result!.Points);
        Assert.True(result.Points.Count >= 4);

        // Check that dilation expanded the polygon
        var originalBounds = GetBounds(polygon);
        var dilatedBounds = GetBounds(result);

        Assert.True(dilatedBounds.Width > originalBounds.Width);
        Assert.True(dilatedBounds.Height > originalBounds.Height);

        // Center should be approximately the same
        var originalCenter = GetCenter(polygon);
        var dilatedCenter = GetCenter(result);

        Assert.True(Math.Abs(originalCenter.X - dilatedCenter.X) < 2);
        Assert.True(Math.Abs(originalCenter.Y - dilatedCenter.Y) < 2);
    }

    [Fact]
    public void Dilate_Rectangle_ExpandsPolygon()
    {
        // 20x5 rectangle (area = 100)
        var polygon = new Polygon([(0, 0), (20, 0), (20, 5), (0, 5)]);

        var result = polygon.Dilate(DefaultDilationRatio);

        Assert.NotEmpty(result!.Points);
        Assert.True(result.Points.Count >= 4);

        var originalBounds = GetBounds(polygon);
        var dilatedBounds = GetBounds(result);

        Assert.True(dilatedBounds.Width > originalBounds.Width);
        Assert.True(dilatedBounds.Height > originalBounds.Height);
    }

    [Fact]
    public void Dilate_LargeSquare_ProducesProportionalDilation()
    {
        // 100x100 square (area = 10000)
        var polygon = new Polygon([(0, 0), (100, 0), (100, 100), (0, 100)]);

        var result = polygon.Dilate(DefaultDilationRatio);

        Assert.NotEmpty(result!.Points);

        // Calculate expected offset: offset = Area * ratio / Perimeter
        // Area = 10000, Perimeter = 400, ratio = 1.5
        // Expected offset = 10000 * 1.5 / 400 = 37.5
        var originalBounds = GetBounds(polygon);
        var dilatedBounds = GetBounds(result);

        var expectedIncrease = 37.5 * 2; // Both sides expand
        var actualWidthIncrease = dilatedBounds.Width - originalBounds.Width;
        var actualHeightIncrease = dilatedBounds.Height - originalBounds.Height;

        // Allow tolerance for rounding and rounded corners
        Assert.True(Math.Abs(actualWidthIncrease - expectedIncrease) < 10);
        Assert.True(Math.Abs(actualHeightIncrease - expectedIncrease) < 10);
    }

    [Fact]
    public void Dilate_NegativeCoordinates_WorksCorrectly()
    {
        var polygon = new Polygon([(-10, -10), (0, -10), (0, 0), (-10, 0)]);

        var result = polygon.Dilate(DefaultDilationRatio);

        Assert.NotEmpty(result!.Points);
        Assert.True(result.Points.Count >= 4);

        var originalBounds = GetBounds(polygon);
        var dilatedBounds = GetBounds(result);

        Assert.True(dilatedBounds.Width > originalBounds.Width);
        Assert.True(dilatedBounds.Height > originalBounds.Height);
    }

    [Fact]
    public void Dilate_LargeCoordinates_WorksCorrectly()
    {
        var polygon = new Polygon([(1000, 1000), (1100, 1000), (1100, 1100), (1000, 1100)]);

        var result = polygon.Dilate(DefaultDilationRatio);

        Assert.NotEmpty(result!.Points);
        Assert.True(result.Points.Count >= 4);

        // Verify coordinates are in reasonable range
        foreach (var point in result.Points)
        {
            Assert.True(point.X >= 900 && point.X <= 1200);
            Assert.True(point.Y >= 900 && point.Y <= 1200);
        }
    }

    [Fact]
    public void Dilate_ConsistentResults_SameInputSameOutput()
    {
        var polygon = new Polygon([(0, 0), (10, 0), (10, 10), (0, 10)]);

        var result1 = polygon.Dilate(DefaultDilationRatio);
        var result2 = polygon.Dilate(DefaultDilationRatio);

        Assert.NotEmpty(result1!.Points);
        Assert.NotEmpty(result2!.Points);
        Assert.Equal(result1.Points.Count, result2.Points.Count);

        for (int i = 0; i < result1.Points.Count; i++)
        {
            Assert.Equal(result1.Points[i], result2.Points[i]);
        }
    }

    // Helper methods
    private static (double X, double Y, double Width, double Height) GetBounds(Polygon polygon)
    {
        var points = polygon.Points;
        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);

        return (minX, minY, maxX - minX, maxY - minY);
    }

    private static (double X, double Y) GetCenter(Polygon polygon)
    {
        var points = polygon.Points;
        return (points.Average(p => p.X), points.Average(p => p.Y));
    }
}
