// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Ocr.Algorithms;
using Ocr.Geometry;

namespace Ocr.Test.Algorithms;

public class TraceBoundaryTests
{
    [Fact]
    public void TraceBoundary_LargeRectangle_ReturnsOrderedBoundary()
    {
        // Arrange - 7x7 grid with 5x5 solid rectangle
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
        var boundary = reliefMap.TraceBoundary((1, 1));

        // Assert
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
    public void TraceBoundary_RectangleAtEdge_HandlesEdgesCorrectly()
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
        var boundary = reliefMap.TraceBoundary((0, 1));

        // Assert
        Assert.NotEmpty(boundary.Points);

        // Should include left edge pixels
        Assert.True(boundary.Points.Any(p => p.X == 0), "Should have left edge pixels");

        // Should not include pixels off of the edge
        Assert.False(boundary.Points.Any(p => p.X < 0), "Should not have pixels off of the edge");

        VerifyBoundaryConnectivity(boundary);
    }

    [Fact]
    public void TraceBoundary_ComplexShape_ReturnsValidBoundary()
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
        var boundary = reliefMap.TraceBoundary((0, 0));

        // Assert
        Assert.True(boundary.Points.Count > 4,
            $"Complex shape should have more than 4 boundary points, got {boundary.Points.Count}");

        VerifyBoundaryConnectivity(boundary);
    }

    [Fact]
    public void TraceBoundary_SinglePixel_ReturnsSinglePoint()
    {
        // Arrange
        float[] data =
        [
            0f, 0f, 0f,
            0f, 1f, 0f,
            0f, 0f, 0f
        ];
        var reliefMap = new ReliefMap(data, width: 3, height: 3);

        // Act
        var boundary = reliefMap.TraceBoundary((1, 1));

        // Assert
        Assert.Single(boundary.Points);
        Assert.Equal((Point)(1, 1), boundary.Points[0]);
    }

    [Fact]
    public void TraceBoundary_ThickHorizontalBar_ReturnsOrderedBoundary()
    {
        // Arrange - 2-pixel-thick horizontal bar
        float[] data =
        [
            0f, 0f, 0f, 0f, 0f, 0f,
            0f, 1f, 1f, 1f, 1f, 0f,
            0f, 1f, 1f, 1f, 1f, 0f,
            0f, 0f, 0f, 0f, 0f, 0f
        ];
        var reliefMap = new ReliefMap(data, width: 6, height: 4);

        // Act
        var boundary = reliefMap.TraceBoundary((1, 1));

        // Assert
        Assert.NotEmpty(boundary.Points);
        VerifyBoundaryConnectivity(boundary);

        // All boundary points should be on the perimeter of the bar
        Assert.True(boundary.Points.All(p => p.X >= 1 && p.X <= 4 && p.Y >= 1 && p.Y <= 2));
    }

    [Fact]
    public void TraceBoundary_ThickVerticalBar_ReturnsOrderedBoundary()
    {
        // Arrange - 2-pixel-thick vertical bar
        float[] data =
        [
            0f, 0f, 0f, 0f,
            0f, 1f, 1f, 0f,
            0f, 1f, 1f, 0f,
            0f, 1f, 1f, 0f,
            0f, 1f, 1f, 0f,
            0f, 0f, 0f, 0f
        ];
        var reliefMap = new ReliefMap(data, width: 4, height: 6);

        // Act
        var boundary = reliefMap.TraceBoundary((1, 1));

        // Assert
        Assert.NotEmpty(boundary.Points);
        VerifyBoundaryConnectivity(boundary);

        // All boundary points should be on the perimeter of the bar
        Assert.True(boundary.Points.All(p => p.X >= 1 && p.X <= 2 && p.Y >= 1 && p.Y <= 4));
    }

    [Fact]
    public void TraceBoundary_TopLeftCorner_HandlesBoundary()
    {
        // Arrange - Shape touching top-left corner
        float[] data =
        [
            1f, 1f, 1f, 0f, 0f,
            1f, 1f, 1f, 0f, 0f,
            1f, 1f, 1f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f
        ];
        var reliefMap = new ReliefMap(data, width: 5, height: 5);

        // Act
        var boundary = reliefMap.TraceBoundary((0, 0));

        // Assert
        Assert.NotEmpty(boundary.Points);
        Assert.Contains((Point)(0, 0), boundary.Points);
        VerifyBoundaryConnectivity(boundary);

        // Should not have any negative coordinates
        Assert.DoesNotContain(boundary.Points, p => p.X < 0 || p.Y < 0);
    }

    [Fact]
    public void TraceBoundary_BottomRightCorner_HandlesBoundary()
    {
        // Arrange - Shape touching bottom-right corner
        float[] data =
        [
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f,
            0f, 0f, 1f, 1f, 1f,
            0f, 0f, 1f, 1f, 1f,
            0f, 0f, 1f, 1f, 1f
        ];
        var reliefMap = new ReliefMap(data, width: 5, height: 5);

        // Act
        var boundary = reliefMap.TraceBoundary((4, 4));

        // Assert
        Assert.NotEmpty(boundary.Points);
        Assert.Contains((Point)(4, 4), boundary.Points);
        VerifyBoundaryConnectivity(boundary);

        // Should not exceed map bounds
        Assert.DoesNotContain(boundary.Points, p => p.X >= 5 || p.Y >= 5);
    }

    [Fact]
    public void TraceBoundary_TwoByTwoSquare_TracesCorrectly()
    {
        // Arrange
        float[] data =
        [
            0f, 0f, 0f, 0f,
            0f, 1f, 1f, 0f,
            0f, 1f, 1f, 0f,
            0f, 0f, 0f, 0f
        ];
        var reliefMap = new ReliefMap(data, width: 4, height: 4);

        // Act
        var boundary = reliefMap.TraceBoundary((1, 1));

        // Assert
        Assert.NotEmpty(boundary.Points);
        VerifyBoundaryConnectivity(boundary);

        // All boundary points should be part of the 2x2 square
        Assert.True(boundary.Points.All(p => p.X >= 1 && p.X <= 2 && p.Y >= 1 && p.Y <= 2));
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
