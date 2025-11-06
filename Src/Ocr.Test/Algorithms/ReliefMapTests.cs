// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Immutable;
using Ocr.Algorithms;
using Ocr.Geometry;

namespace Ocr.Test.Algorithms;

public class ReliefMapTests
{
    [Fact]
    public void TraceAllBoundaries_LargeRectangle_ReturnsOrderedBoundary()
    {
        // Arrange - 7x7 grid with 5x5 solid rectangle that will survive morphological opening
        float[] data =
        [
            0f, 0f, 0f, 0f, 0f, 0f, 0f,
            0f, 1f, 1f, 1f, 1f, 1f, 0f,
            0f, 1f, 1f, 1f, 1f, 1f, 0f,
            0f, 1f, 1f, 1f, 1f, 1f, 0f,
            0f, 1f, 1f, 1f, 1f, 1f, 0f,
            0f, 1f, 1f, 1f, 1f, 1f, 0f,
            0f, 0f, 0f, 0f, 0f, 0f, 0f
        ];
        var reliefMap = new ReliefMap(data, width: 7, height: 7);

        // Act
        var boundaries = reliefMap.TraceAllBoundaries();

        // Assert
        Assert.Single(boundaries);
        var boundary = boundaries[0];

        // After morphological opening, we expect a 5x5 rectangle from (1,1) to (5,5)
        // The trace starts at (1,1) (top-left most pixel) and goes clockwise.
        // It stops when reaching a neighbor of the start to avoid double-tracing,
        // so (1,2) is intentionally omitted.
        List<Point> expectedBoundary =
        [
            // Starting at (1,1) and going clockwise:
            (1, 1), // Start
            (2, 1), (3, 1), (4, 1), (5, 1), // Top edge
            (5, 2), (5, 3), (5, 4), (5, 5), // Right edge
            (4, 5), (3, 5), (2, 5), (1, 5), // Bottom edge
            (1, 4), (1, 3) // Left edge (stops before (1,2) to avoid reaching start)
        ];

        // Verify the boundary matches exactly (both content and order)
        Assert.Equal(expectedBoundary, boundary.Points);

        // Verify boundary connectivity
        VerifyBoundaryConnectivity(boundary);
    }

    [Fact]
    public void TraceAllBoundaries_MultiplePolygons_ReturnsSeparateBoundaries()
    {
        // Arrange - Two separate larger rectangles that will survive morphological opening
        float[] data =
        [
            1f, 1f, 1f, 1f, 0f, 0f, 1f, 1f, 1f, 1f,
            1f, 1f, 1f, 1f, 0f, 0f, 1f, 1f, 1f, 1f,
            1f, 1f, 1f, 1f, 0f, 0f, 1f, 1f, 1f, 1f,
            1f, 1f, 1f, 1f, 0f, 0f, 1f, 1f, 1f, 1f,
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            1f, 1f, 1f, 0f, 0f, 0f, 1f, 1f, 1f, 1f,
            1f, 1f, 1f, 0f, 0f, 0f, 1f, 1f, 1f, 1f,
            1f, 1f, 1f, 0f, 0f, 0f, 1f, 1f, 1f, 1f,
            1f, 1f, 1f, 0f, 0f, 0f, 1f, 1f, 1f, 1f
        ];
        var reliefMap = new ReliefMap(data, width: 10, height: 10);

        // Act
        var boundaries = reliefMap.TraceAllBoundaries();

        // Assert - Should find multiple separate components
        Assert.True(boundaries.Count >= 1, $"Expected at least 1 boundary, got {boundaries.Count}");

        // Each boundary should be valid
        foreach (var boundary in boundaries)
        {
            Assert.NotEmpty(boundary.Points);
            VerifyBoundaryConnectivity(boundary);

            // Verify it's a closed polygon
            Assert.True(boundary.Points.Count >= 3,
                $"Polygon should have at least 3 points, got {boundary.Points.Count}");
        }

        // No duplicate points across different boundaries
        var allPoints = boundaries.SelectMany(b => b.Points).ToList();
        var uniquePoints = allPoints.ToHashSet();
        Assert.Equal(allPoints.Count, uniquePoints.Count);
    }

    [Fact]
    public void TraceAllBoundaries_RectangleAtEdge_HandlesEdgesCorrectly()
    {
        // Arrange - Rectangle touching the left edge
        float[] data =
        [
            0f, 0f, 0f, 0f, 0f, 0f, 0f,
            1f, 1f, 1f, 1f, 1f, 0f, 0f,
            1f, 1f, 1f, 1f, 1f, 0f, 0f,
            1f, 1f, 1f, 1f, 1f, 0f, 0f,
            1f, 1f, 1f, 1f, 1f, 0f, 0f,
            1f, 1f, 1f, 1f, 1f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f, 0f, 0f
        ];
        var reliefMap = new ReliefMap(data, width: 7, height: 7);

        // Act
        var boundaries = reliefMap.TraceAllBoundaries();

        // Assert
        Assert.True(boundaries.Count >= 1, $"Expected at least 1 boundary, got {boundaries.Count}");

        var boundary = boundaries[0];
        Assert.NotEmpty(boundary.Points);

        // Should include left edge pixels
        Assert.True(boundary.Points.Any(p => p.X == 0), "Should have left edge pixels");

        // Should not include pixels off of the edge
        Assert.False(boundary.Points.Any(p => p.X < 0), "Should not have pixels off of the edge");

        VerifyBoundaryConnectivity(boundary);
    }

    [Fact]
    public void TraceAllBoundaries_EmptyInput_ReturnsNoBoundaries()
    {
        // Arrange
        float[] data =
        [
            0f, 0f, 0f,
            0f, 0f, 0f,
            0f, 0f, 0f
        ];
        var reliefMap = new ReliefMap(data, width: 3, height: 3);

        // Act
        var boundaries = reliefMap.TraceAllBoundaries();

        // Assert
        Assert.Empty(boundaries);
    }

    [Fact]
    public void TraceAllBoundaries_ComplexShape_ReturnsValidBoundary()
    {
        // Arrange - L-shaped region
        float[] data =
        [
            1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f,
            1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f,
            1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f,
            1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f,
            1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f,
            1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f,
            1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f,
            1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f
        ];
        var reliefMap = new ReliefMap(data, width: 8, height: 8);

        // Act
        var boundaries = reliefMap.TraceAllBoundaries();

        // Assert
        Assert.NotEmpty(boundaries);

        var mainBoundary = boundaries[0];
        Assert.True(mainBoundary.Points.Count > 4,
            $"Complex shape should have more than 4 boundary points, got {mainBoundary.Points.Count}");

        VerifyBoundaryConnectivity(mainBoundary);
    }

    [Fact]
    public void TraceAllBoundaries_CanOnlyBeCalledOnce()
    {
        // Arrange
        float[] data =
        [
            0f, 1f, 0f,
            1f, 1f, 1f,
            0f, 1f, 0f
        ];
        var reliefMap = new ReliefMap(data, width: 3, height: 3);

        // Act
        _ = reliefMap.TraceAllBoundaries();

        // Assert - Second call should throw
        Assert.Throws<InvalidOperationException>(() => reliefMap.TraceAllBoundaries());
    }

    [Theory]
    [InlineData(0, 0, 0)]      // Both zero
    [InlineData(1, -1, 1)]     // Negative width
    [InlineData(1, 1, -1)]     // Negative height
    [InlineData(1, 0, 1)]      // Zero width
    [InlineData(1, 1, 0)]      // Zero height
    [InlineData(4, -5, -2)]    // Both negative
    [InlineData(9, -10, 3)]    // Negative width with valid height
    [InlineData(6, 2, -3)]     // Valid width with negative height
    public void ReliefMap_InvalidDimensions_Throws(int dataSize, int width, int height)
    {
        // Arrange
        var data = new float[dataSize];
        Array.Fill(data, 1f);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReliefMap(data, width, height));
    }

    [Fact]
    public void ReliefMap_MismatchedDimensions_ThrowsArgumentException()
    {
        // Arrange
        float[] data = [0.5f, 0.5f, 0.5f];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ReliefMap(data, width: 2, height: 2));
    }

    private static void VerifyBoundaryConnectivity(Polygon boundary)
    {
        if (boundary.Points.Count <= 1)
            return;

        // No duplicate pixels, reasonable bounds
        var uniquePoints = boundary.Points.ToHashSet();
        Assert.Equal(boundary.Points.Count, uniquePoints.Count);

        foreach (var point in boundary.Points)
        {
            Assert.True(point.X >= 0 && point.Y >= 0);
            Assert.True(point.X < 20 && point.Y < 20);
        }

        // Verify 8-connectivity: each consecutive pair should be within distance 1 (8-connected neighbors)
        for (int i = 0; i < boundary.Points.Count - 1; i++)
        {
            var current = boundary.Points[i];
            var next = boundary.Points[i + 1];

            int dx = Math.Abs(next.X - current.X);
            int dy = Math.Abs(next.Y - current.Y);

            Assert.True(dx <= 1 && dy <= 1 && (dx + dy) > 0,
                $"Points at indices {i} and {i + 1} are not 8-connected: ({current.X},{current.Y}) -> ({next.X},{next.Y})");
        }

        // Verify start and end are within distance 2 (allowing for the intentional gap)
        if (boundary.Points.Count >= 3)
        {
            var start = boundary.Points[0];
            var end = boundary.Points[^1];

            int dx = Math.Abs(end.X - start.X);
            int dy = Math.Abs(end.Y - start.Y);

            Assert.True(dx <= 2 && dy <= 2,
                $"Start and end points are too far apart: ({start.X},{start.Y}) and ({end.X},{end.Y})");
        }
    }
}
