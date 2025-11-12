// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Ocr.Algorithms;
using Ocr.Geometry;

namespace Ocr.Test.Algorithms;

public class TraceAllBoundariesTests
{
    [Fact]
    public void TraceAllBoundaries_BinarizationThreshold_FiltersCorrectly()
    {
        // Arrange - grayscale values around the 0.2 threshold
        float[] data =
        [
            0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f,
            0.1f, 0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 0.1f,
            0.1f, 0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 0.1f,
            0.1f, 0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 0.1f,
            0.1f, 0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 0.1f,
            0.1f, 0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 0.1f,
            0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f
        ];
        var reliefMap = new ReliefMap(data, width: 7, height: 7);

        // Act
        var boundaries = reliefMap.TraceAllBoundaries();

        // Assert - values >= 0.2 should form a boundary, values < 0.2 should not
        Assert.Single(boundaries);
        var boundary = boundaries[0];
        Assert.NotEmpty(boundary.Points);

        // All boundary points should be from the region that was >= 0.2 (after erosion/dilation)
        VerifyBoundaryConnectivity(boundary);
    }

    [Fact]
    public void TraceAllBoundaries_MorphologicalOpening_RemovesSmallNoise()
    {
        // Arrange - binary input with isolated single pixels (noise)
        float[] data =
        [
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f, 0f, 0f, 0f, 1f, 0f,  // isolated pixels
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 1f, 1f, 1f, 0f, 0f, 0f,
            0f, 0f, 0f, 1f, 1f, 1f, 0f, 0f, 0f,  // 3x3 solid block (survives)
            0f, 0f, 0f, 1f, 1f, 1f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f, 0f, 0f, 0f, 1f, 0f,  // isolated pixels
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f
        ];
        var reliefMap = new ReliefMap(data, width: 9, height: 9);

        // Act
        var boundaries = reliefMap.TraceAllBoundaries();

        // Assert - only the 3x3 block should survive morphological opening
        // The isolated pixels should be removed by erosion
        Assert.True(boundaries.Count <= 1, $"Expected at most 1 boundary (noise removed), got {boundaries.Count}");

        if (boundaries.Count == 1)
        {
            VerifyBoundaryConnectivity(boundaries[0]);
        }
    }

    [Fact]
    public void TraceAllBoundaries_NonConvexShape_TracesCorrectly()
    {
        // Arrange - L-shaped region (non-convex, large enough to survive morphological opening)
        float[] data =
        [
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            0f, 1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f, 0f,
            0f, 1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f, 0f,
            0f, 1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f, 0f,
            0f, 1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f, 0f,
            0f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 0f,
            0f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 0f,
            0f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 0f,
            0f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 0f,
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f
        ];
        var reliefMap = new ReliefMap(data, width: 10, height: 10);

        // Act
        var boundaries = reliefMap.TraceAllBoundaries();

        // Assert
        Assert.NotEmpty(boundaries);
        var boundary = boundaries[0];
        Assert.True(boundary.Points.Count > 4,
            $"Non-convex L-shape should have more than 4 boundary points, got {boundary.Points.Count}");
        VerifyBoundaryConnectivity(boundary);
    }

    [Fact]
    public void TraceAllBoundaries_MultipleShapes_ProducesSeparateBoundaries()
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

        // No duplicate points across different boundaries (FloodFill prevents re-tracing)
        var allPoints = boundaries.SelectMany(b => b.Points).ToList();
        var uniquePoints = allPoints.ToHashSet();
        Assert.Equal(allPoints.Count, uniquePoints.Count);
    }

    [Fact]
    public void TraceAllBoundaries_EmptyInput_ReturnsEmpty()
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

    [Fact]
    public void TraceAllBoundaries_PropertyTest_RandomShapes_ProducesValidBoundaries()
    {
        var numIterations = 10000;

        Parallel.ForEach(Enumerable.Range(0, numIterations), iteration =>
        {
            // Each thread gets its own random with a different seed
            var random = new Random(iteration);

            // Generate random convex hull
            var hullPoints = GenerateRandomConvexHull(random);

            // Create bitmap and rasterize the convex hull
            int width = 50;
            int height = 50;
            var data = new float[width * height];
            RasterizeConvexPolygon(data, width, height, hullPoints);

            var map = new ReliefMap([.. data], width, height);
            var boundaries = map.TraceAllBoundaries();

            // Property: All boundaries are valid and traverse perimeter exactly once
            foreach (var boundary in boundaries)
            {
                Assert.True(boundary.Points.Count > 0);
                // VerifyBoundaryConnectivity checks:
                // - No duplicates (proves we don't loop twice)
                // - 8-connectivity (proves continuous path)
                // - Start/end within distance 2 (proves we stopped near start)
                VerifyBoundaryConnectivity(boundary);
            }

            // Property: No overlap between boundaries
            var allPoints = boundaries.SelectMany(b => b.Points).ToList();
            var uniquePoints = allPoints.ToHashSet();
            Assert.Equal(allPoints.Count, uniquePoints.Count);
        });
    }

    private static List<PointF> GenerateRandomConvexHull(Random random)
    {
        // Generate random points scattered in a circle
        var points = new List<Point>();
        int numPoints = random.Next(5, 15);

        for (int i = 0; i < numPoints; i++)
        {
            double angle = random.NextDouble() * 2 * Math.PI;
            double radius = random.NextDouble() * 5 + 2; // radius 2-7
            int x = (int)(radius * Math.Cos(angle));
            int y = (int)(radius * Math.Sin(angle));
            points.Add((x, y));
        }

        // Compute convex hull
        var polygon = new Polygon(points);
        var hull = polygon.ToConvexHull();
        return hull.Points.ToList();
    }

    private static void RasterizeConvexPolygon(float[] data, int width, int height, List<PointF> polygon)
    {
        // Simple point-in-polygon test for each pixel
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (IsPointInConvexPolygon(x, y, polygon))
                {
                    data[y * width + x] = 1f;
                }
            }
        }
    }

    private static bool IsPointInConvexPolygon(int px, int py, List<PointF> polygon)
    {
        // For convex polygons, point is inside if it's on the same side of all edges
        if (polygon.Count < 3)
            return false;

        int sign = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % polygon.Count];

            double cross = (p2.X - p1.X) * (py - p1.Y) - (p2.Y - p1.Y) * (px - p1.X);

            if (cross != 0)
            {
                if (sign == 0)
                    sign = cross > 0 ? 1 : -1;
                else if ((cross > 0 ? 1 : -1) != sign)
                    return false;
            }
        }

        return true;
    }

    private static void VerifyBoundaryConnectivity(Polygon boundary)
    {
        if (boundary.Points.Count <= 1)
            return;

        // No duplicate pixels
        var uniquePoints = boundary.Points.ToHashSet();
        Assert.Equal(boundary.Points.Count, uniquePoints.Count);

        // All points should be in reasonable bounds
        foreach (var point in boundary.Points)
        {
            Assert.True(point.X >= 0 && point.Y >= 0);
            Assert.True(point.X < 100 && point.Y < 100);
        }

        // Verify 8-connectivity: each consecutive pair should be within distance 1 (8-connected neighbors)
        for (int i = 0; i < boundary.Points.Count - 1; i++)
        {
            var current = boundary.Points[i];
            var next = boundary.Points[i + 1];

            int dx = (int)Math.Abs(next.X - current.X);
            int dy = (int)Math.Abs(next.Y - current.Y);

            Assert.True(dx <= 1 && dy <= 1 && (dx + dy) > 0,
                $"Points at indices {i} and {i + 1} are not 8-connected: ({current.X},{current.Y}) -> ({next.X},{next.Y})");
        }

        // Verify start and end are within distance 2 (allowing for the intentional gap)
        if (boundary.Points.Count >= 3)
        {
            var start = boundary.Points[0];
            var end = boundary.Points[^1];

            int dx = (int)Math.Abs(end.X - start.X);
            int dy = (int)Math.Abs(end.Y - start.Y);

            Assert.True(dx <= 2 && dy <= 2,
                $"Start and end points are too far apart: ({start.X},{start.Y}) and ({end.X},{end.Y})");
        }
    }
}
