// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Ocr.Geometry;

namespace Ocr.Test.Geometry;

public class RotatedRectangleTests
{
    #region ToRotatedRectangle
    [Fact]
    public void ToRotatedRectangle_WithLessThan3Points_ReturnsNull()
    {
        var hull0 = new ConvexHull { Points = [] };
        var hull1 = new ConvexHull { Points = [(5, 3)] };
        var hull2 = new ConvexHull { Points = [(0, 0), (5, 5)] };

        Assert.Null(hull0.ToRotatedRectangle());
        Assert.Null(hull1.ToRotatedRectangle());
        Assert.Null(hull2.ToRotatedRectangle());
    }

    [Fact]
    public void ToRotatedRectangle_WithCollinearPoints_ReturnsNull()
    {
        var horizontalLine = new ConvexHull { Points = [(0, 5), (5, 5), (10, 5)] };
        var verticalLine = new ConvexHull { Points = [(5, 0), (5, 5), (5, 10)] };
        var diagonalLine = new ConvexHull { Points = [(0, 0), (5, 5), (10, 10)] };

        Assert.Null(horizontalLine.ToRotatedRectangle());
        Assert.Null(verticalLine.ToRotatedRectangle());
        Assert.Null(diagonalLine.ToRotatedRectangle());
    }

    [Fact]
    public void ToRotatedRectangle_WithDuplicatePoints_ReturnsNull()
    {
        var allSame = new ConvexHull { Points = [(5, 5), (5, 5), (5, 5)] };
        var mostlySame = new ConvexHull { Points = [(5, 5), (5, 5), (5, 5.0001)] };

        Assert.Null(allSame.ToRotatedRectangle());
        Assert.Null(mostlySame.ToRotatedRectangle());
    }

    [Fact]
    public void ToRotatedRectangle_WithTriangle_ReturnsValidRectangle()
    {
        var hull = new ConvexHull { Points = [(0, 0), (10, 0), (5, 8)] };

        var result = hull.ToRotatedRectangle();

        Assert.NotNull(result);
        var corners = result.Corners().Points;
        Assert.Equal(4, corners.Count);

        VerifyRectangleHasParallelSides(corners.ToList());
        VerifyAllPointsContained(hull.Points.ToList(), result);
        VerifyAtLeastTwoPointsOnBoundary(hull.Points.ToList(), corners.ToList());
    }

    [Fact]
    public void ToRotatedRectangle_WithAxisAlignedSquare_ReturnsSquare()
    {
        var hull = new ConvexHull { Points = [(0, 0), (10, 0), (10, 10), (0, 10)] };

        var result = hull.ToRotatedRectangle();

        Assert.NotNull(result);
        var corners = result.Corners().Points;

        VerifyRectangleHasParallelSides(corners.ToList());
        VerifyAllPointsContained(hull.Points.ToList(), result);

        Assert.True(Math.Abs(result.Width - 10) < 1.0, $"Expected width ~10, got {result.Width}");
        Assert.True(Math.Abs(result.Height - 10) < 1.0, $"Expected height ~10, got {result.Height}");
    }

    [Fact]
    public void ToRotatedRectangle_WithRotated45Square_ReturnsSquare()
    {
        var hull = new ConvexHull { Points = [(50, 0), (100, 50), (50, 100), (0, 50)] };

        var result = hull.ToRotatedRectangle();

        Assert.NotNull(result);
        var corners = result.Corners().Points;

        VerifyRectangleHasParallelSides(corners.ToList());
        VerifyAllPointsContained(hull.Points.ToList(), result);

        var expectedSide = Math.Sqrt(50 * 50 + 50 * 50);
        Assert.True(Math.Abs(result.Width - expectedSide) < 1.0,
            $"Expected width ~{expectedSide:F1}, got {result.Width}");
        Assert.True(Math.Abs(result.Height - expectedSide) < 1.0,
            $"Expected height ~{expectedSide:F1}, got {result.Height}");
    }

    [Fact]
    public void ToRotatedRectangle_WithAxisAlignedRectangle_ReturnsRectangle()
    {
        var hull = new ConvexHull { Points = [(0, 0), (20, 0), (20, 10), (0, 10)] };

        var result = hull.ToRotatedRectangle();

        Assert.NotNull(result);
        var corners = result.Corners().Points;

        VerifyRectangleHasParallelSides(corners.ToList());
        VerifyAllPointsContained(hull.Points.ToList(), result);

        Assert.True(Math.Abs(result.Width - 20) < 1.0, $"Expected width ~20, got {result.Width}");
        Assert.True(Math.Abs(result.Height - 10) < 1.0, $"Expected height ~10, got {result.Height}");
    }

    [Fact]
    public void ToRotatedRectangle_WithRegularPentagon_ReturnsValidRectangle()
    {
        var hull = new ConvexHull
        {
            Points =
            [
                (3, 0),
                (5, 1),
                (4, 3),
                (2, 3),
                (1, 1)
            ]
        };

        var result = hull.ToRotatedRectangle();

        Assert.NotNull(result);
        var corners = result.Corners().Points;

        VerifyRectangleHasParallelSides(corners.ToList());
        VerifyAllPointsContained(hull.Points.ToList(), result);
        VerifyAtLeastTwoPointsOnBoundary(hull.Points.ToList(), corners.ToList());
    }

    [Fact]
    public void ToRotatedRectangle_WithRandomPointCloud_ProducesValidMBR()
    {
        var random = new Random(0);
        var numIterations = 10000;

        for (int n = 0; n < numIterations; n++)
        {
            var points = new List<PointF>();

            for (int i = 0; i < 50; i++)
            {
                double angle = random.NextDouble() * 2 * Math.PI;
                double radius = random.NextDouble() * 80;
                double x = 100 + radius * Math.Cos(angle);
                double y = 75 + radius * Math.Sin(angle);
                points.Add((x, y));
            }

            var polygon = new Polygon(points);
            var convexHull = polygon.ToConvexHull();
            Assert.NotNull(convexHull);

            var rotatedRect = convexHull.ToRotatedRectangle();
            Assert.NotNull(rotatedRect);

            var corners = rotatedRect.Corners().Points;

            Assert.Equal(4, corners.Count);
            Assert.True(convexHull.Points.Count >= 3);

            VerifyRectangleHasParallelSides(corners.ToList());
            VerifyAllPointsContained(convexHull.Points.ToList(), rotatedRect);
            VerifyAtLeastTwoPointsOnBoundary(convexHull.Points.ToList(), corners.ToList());
        }
    }

    [Fact]
    public void ToRotatedRectangle_ReturnsMinimumAreaRectangle()
    {
        var hull = new ConvexHull { Points = [(0, 0), (10, 0), (10, 2), (0, 2)] };

        var result = hull.ToRotatedRectangle();

        Assert.NotNull(result);

        var area = result.Width * result.Height;
        Assert.True(Math.Abs(area - 20) < 1.0, $"Expected area ~20, got {area}");
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
    #endregion ToRotatedRectangle

    #region Corners
    [Fact]
    public void Corners_WithAxisAlignedRectangle_ReturnsCorrectCorners()
    {
        var rect = new RotatedRectangle
        {
            X = 10,
            Y = 20,
            Width = 50,
            Height = 30,
            Angle = 0
        };

        var corners = rect.Corners().Points;

        Assert.Equal(4, corners.Count);
        AssertPointEquals((10, 20), corners[0], "top-left");
        AssertPointEquals((60, 20), corners[1], "top-right");
        AssertPointEquals((60, 50), corners[2], "bottom-right");
        AssertPointEquals((10, 50), corners[3], "bottom-left");
    }

    [Fact]
    public void Corners_WithPi4RotatedSquare_ReturnsCorrectCorners()
    {
        var rect = new RotatedRectangle
        {
            X = 0,
            Y = 0,
            Width = 1,
            Height = 1,
            Angle = Math.PI / 4
        };

        var corners = rect.Corners().Points;

        Assert.Equal(4, corners.Count);

        var sqrt2over2 = Math.Sqrt(2) / 2;
        AssertPointEquals((0, 0), corners[0], "top-left");
        AssertPointEquals((sqrt2over2, sqrt2over2), corners[1], "top-right");
        AssertPointEquals((0, Math.Sqrt(2)), corners[2], "bottom-right");
        AssertPointEquals((-sqrt2over2, sqrt2over2), corners[3], "bottom-left");
    }

    [Fact]
    public void Corners_RoundTrip_PreservesRectangleShape()
    {
        var random = new Random(0);
        var numIterations = 10000;

        for (int n = 0; n < numIterations; n++)
        {
            var x = random.NextDouble() * 100;
            var y = random.NextDouble() * 100;
            var width = 10 + random.NextDouble() * 90;
            var height = 10 + random.NextDouble() * 90;
            var angle = (random.NextDouble() - 0.5) * 2 * Math.PI;

            var original = new RotatedRectangle
            {
                X = x,
                Y = y,
                Width = width,
                Height = height,
                Angle = angle
            };

            var originalCorners = original.Corners().Points;
            var reconstructed = new RotatedRectangle(originalCorners.ToList());
            var reconstructedCorners = reconstructed.Corners().Points;

            Assert.Equal(4, reconstructedCorners.Count);

            // Corners should be the same set of points (order-independent)
            foreach (var originalCorner in originalCorners)
            {
                bool found = false;
                foreach (var reconstructedCorner in reconstructedCorners)
                {
                    if (Math.Abs(originalCorner.X - reconstructedCorner.X) < 1e-6 &&
                        Math.Abs(originalCorner.Y - reconstructedCorner.Y) < 1e-6)
                    {
                        found = true;
                        break;
                    }
                }
                Assert.True(found, $"Original corner ({originalCorner.X}, {originalCorner.Y}) not found in reconstructed corners");
            }
        }
    }

    private void AssertPointEquals((double X, double Y) expected, PointF actual, string label, double tolerance = 1e-10)
    {
        Assert.True(Math.Abs(expected.X - actual.X) < tolerance,
            $"{label} X mismatch: expected {expected.X:F10}, got {actual.X:F10}");
        Assert.True(Math.Abs(expected.Y - actual.Y) < tolerance,
            $"{label} Y mismatch: expected {expected.Y:F10}, got {actual.Y:F10}");
    }

    private void AssertRectangleEquals(RotatedRectangle expected, RotatedRectangle actual, double tolerance)
    {
        Assert.True(Math.Abs(expected.X - actual.X) < tolerance,
            $"X mismatch: expected {expected.X:F10}, got {actual.X:F10}");
        Assert.True(Math.Abs(expected.Y - actual.Y) < tolerance,
            $"Y mismatch: expected {expected.Y:F10}, got {actual.Y:F10}");
        Assert.True(Math.Abs(expected.Width - actual.Width) < tolerance,
            $"Width mismatch: expected {expected.Width:F10}, got {actual.Width:F10}");
        Assert.True(Math.Abs(expected.Height - actual.Height) < tolerance,
            $"Height mismatch: expected {expected.Height:F10}, got {actual.Height:F10}");
        Assert.True(Math.Abs(expected.Angle - actual.Angle) < tolerance,
            $"Angle mismatch: expected {expected.Angle:F10}, got {actual.Angle:F10}");
    }
    #endregion Corners

    #region Utils
    private bool AreVectorsParallel((double X, double Y) v1, (double X, double Y) v2, double tolerance)
    {
        double crossProduct = v1.X * v2.Y - v1.Y * v2.X;
        return Math.Abs(crossProduct) < tolerance;
    }

    private void VerifyAllPointsContained(List<PointF> points, RotatedRectangle rect)
    {
        foreach (var point in points)
        {
            Assert.True(IsPointInRotatedRect(point, rect),
                $"Point {point} should be contained within the oriented rectangle");
        }
    }

    private void VerifyAtLeastTwoPointsOnBoundary(List<PointF> points, List<PointF> rectangleCorners)
    {
        int pointsOnBoundary = 0;
        var tolerance = 1.0;

        foreach (var point in points)
        {
            if (IsPointOnRectangleBoundary(point, rectangleCorners, tolerance))
            {
                pointsOnBoundary++;
            }
        }

        Assert.True(pointsOnBoundary >= 2,
            $"At least 2 original points should lie within {tolerance} pixels of the rectangle boundary. Found {pointsOnBoundary} points on boundary.");
    }

    private bool IsPointOnRectangleBoundary(PointF point, List<PointF> rectangle, double tolerance)
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

    private bool IsPointInRotatedRect(PointF point, RotatedRectangle rect)
    {
        var padding = 1.0;

        var translatedX = point.X - rect.X;
        var translatedY = point.Y - rect.Y;

        var cos = Math.Cos(-rect.Angle);
        var sin = Math.Sin(-rect.Angle);
        var localX = translatedX * cos - translatedY * sin;
        var localY = translatedX * sin + translatedY * cos;

        return localX >= -padding && localX <= rect.Width + padding &&
               localY >= -padding && localY <= rect.Height + padding;
    }
    #endregion Utils
}
