// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Geometry;

public record RotatedRectangle
{
    [JsonPropertyName("x")]
    public required double X { get; init; }  // Top left x

    [JsonPropertyName("y")]
    public required double Y { get; init; }  // Top left y

    [JsonPropertyName("height")]
    public required double Height  // Height
    {
        get;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    [JsonPropertyName("width")]
    public required double Width  // Width
    {
        get;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    [JsonPropertyName("angle")]
    public required double Angle { get; init; }  // Angle in radians

    public RotatedRectangle() { }

    [SetsRequiredMembers]
    public RotatedRectangle(List<PointF> corners)  // corners must be convex and in clockwise order
    {
        if (corners.Count != 4)
            throw new ArgumentException("Must have 4 corners");

        // Set 1 of parallel edges: corners[0] -> corners[1] and corners[2] -> corners[3]
        List<(PointF Start, PointF End)> parallelEdges1 = [(corners[0], corners[1]), (corners[2], corners[3])];

        // Set 2 of parallel edges: corners[1] -> corners[2] and corners[3] -> corners[0]
        List<(PointF Start, PointF End)> parallelEdges2 = [(corners[1], corners[2]), (corners[3], corners[0])];

        // Sort sets of parallel edges by edge length
        List<List<(PointF Start, PointF End)>> parallelEdgeSets = [parallelEdges1, parallelEdges2];
        parallelEdgeSets.Sort((e1, e2) => EdgeLength(e1[0]).CompareTo(EdgeLength(e2[0])));
        var (shortEdges, longEdges) = (parallelEdgeSets[0], parallelEdgeSets[1]);

        // The top edge is the long edge with the lowest Y value
        var topEdge = YMidpoint(longEdges[0]) < YMidpoint(longEdges[1]) ? longEdges[0] : longEdges[1];

        // Find the top left and top right points
        List<PointF> topEdgePoints = [topEdge.Start, topEdge.End];
        topEdgePoints.Sort((p1, p2) => p1.X.CompareTo(p2.X));
        var (topLeft, topRight) = (topEdgePoints[0], topEdgePoints[1]);

        // Calculate angle
        var angle = Math.Atan2(topRight.Y - topLeft.Y, topRight.X - topLeft.X);

        // Calculate height and width
        var height = EdgeLength(shortEdges[0]);
        var width = EdgeLength(longEdges[0]);

        if (Math.Abs(height - width) < 0.000001)  // If it's a square
        {
            // For squares, we always want top left to be point closest to the origin
            // Find the corner closest to origin (0, 0)
            topLeft = corners[0];
            var minDistance = topLeft.X * topLeft.X + topLeft.Y * topLeft.Y;

            for (int i = 1; i < corners.Count; i++)
            {
                var distance = corners[i].X * corners[i].X + corners[i].Y * corners[i].Y;
                if (distance < minDistance)
                {
                    minDistance = distance;
                    topLeft = corners[i];
                }
            }

            // Find the adjacent corners (should be at distance equal to side length from topLeft)
            var sideLength = width; // width == height for square
            PointF topRightCandidate = topLeft;
            bool foundTopRight = false;

            foreach (var corner in corners)
            {
                if (corner.X == topLeft.X && corner.Y == topLeft.Y)
                    continue;

                var dist = Math.Sqrt(Math.Pow(corner.X - topLeft.X, 2) + Math.Pow(corner.Y - topLeft.Y, 2));

                // Check if this is an adjacent corner (side of square, not diagonal)
                if (Math.Abs(dist - sideLength) < 0.001)
                {
                    // Pick the corner with larger X (or if same X, smaller Y) to be "top right"
                    if (!foundTopRight || corner.X > topRightCandidate.X ||
                        (Math.Abs(corner.X - topRightCandidate.X) < 0.001 && corner.Y < topRightCandidate.Y))
                    {
                        topRightCandidate = corner;
                        foundTopRight = true;
                    }
                }
            }

            if (foundTopRight)
            {
                angle = Math.Atan2(topRightCandidate.Y - topLeft.Y, topRightCandidate.X - topLeft.X);
            }
        }

        if (Math.Abs(angle - Math.PI / 2) < 0.00001)
        {
            // When angle is pi/2 (vertical edge going up), we need to use the most extreme top-right virtual corner
            // of the original rectangle as the starting point to ensure proper orientation
            topLeft = new PointF
            {
                X = Math.Max(corners[0].X, Math.Max(corners[1].X, Math.Max(corners[2].X, corners[3].X))),
                Y = Math.Min(corners[0].Y, Math.Min(corners[1].Y, Math.Min(corners[2].Y, corners[3].Y)))
            };
        }
        else if (Math.Abs(angle + Math.PI / 2) < 0.00001)
        {
            // When angle is -pi/2 (vertical edge going down), we need to use the most extreme bottom-left virtual corner
            // of the original rectangle as the starting point to ensure proper orientation
            topLeft = new PointF
            {
                X = Math.Min(corners[0].X, Math.Min(corners[1].X, Math.Min(corners[2].X, corners[3].X))),
                Y = Math.Max(corners[0].Y, Math.Max(corners[1].Y, Math.Max(corners[2].Y, corners[3].Y)))
            };
        }

        X = topLeft.X;
        Y = topLeft.Y;
        Height = height;
        Width = width;
        Angle = angle;

        return;

        double EdgeLength((PointF Start, PointF End) edge) =>
            Math.Sqrt(Math.Pow(edge.End.X - edge.Start.X, 2) + Math.Pow(edge.End.Y - edge.Start.Y, 2));

        double YMidpoint((PointF Start, PointF End) edge) => (edge.Start.Y + edge.End.Y) / 2;
    }

    public Polygon Corners()
    {
        var cos = Math.Cos(Angle);
        var sin = Math.Sin(Angle);

        var topLeft = new PointF
        {
            X = X,
            Y = Y
        };

        var topRight = new PointF
        {
            X = X + Width * cos,
            Y = Y + Width * sin
        };

        var bottomRight = new PointF
        {
            X = X + Width * cos - Height * sin,
            Y = Y + Width * sin + Height * cos
        };

        var bottomLeft = new PointF
        {
            X = X - Height * sin,
            Y = Y + Height * cos
        };

        return new Polygon([topLeft, topRight, bottomRight, bottomLeft]);  // clockwise order
    }

    public Image<Rgb24> Crop(Image<Rgb24> image)
    {
        var corners = Corners().Points;

        var topLeft = corners[0];
        var topRight = corners[1];
        var bottomLeft = corners[3];

        var outputWidth = (int)Math.Ceiling(Width);
        var outputHeight = (int)Math.Ceiling(Height);

        var outputImage = new Image<Rgb24>(outputWidth, outputHeight);

        // Define the local coordinate system using the rectangle edges as basis vectors
        var uVector = (X: topRight.X - topLeft.X, Y: topRight.Y - topLeft.Y);
        var vVector = (X: bottomLeft.X - topLeft.X, Y: bottomLeft.Y - topLeft.Y);

        image.ProcessPixelRows(outputImage, (sourceAccessor, destAccessor) =>
        {
            // For each pixel in the output image
            for (var j = 0; j < outputHeight; j++)
            {
                var destRow = destAccessor.GetRowSpan(j);

                for (var i = 0; i < outputWidth; i++)
                {
                    // Convert output pixel to normalized coordinates [0,1]
                    double u = outputWidth > 1 ? (double)i / (outputWidth - 1) : 0;
                    double v = outputHeight > 1 ? (double)j / (outputHeight - 1) : 0;

                    // Map to source image coordinates
                    var sourceX = topLeft.X + u * uVector.X + v * vVector.X;
                    var sourceY = topLeft.Y + u * uVector.Y + v * vVector.Y;

                    // Sample the source image
                    destRow[i] = BilinearSample(sourceAccessor, sourceX, sourceY);
                }
            }
        });

        return outputImage;

        static Rgb24 BilinearSample(PixelAccessor<Rgb24> sourceAccessor, double x, double y)
        {
            x = Math.Clamp(x, 0, sourceAccessor.Width - 1);
            y = Math.Clamp(y, 0, sourceAccessor.Height - 1);

            // Get the integer coordinates and fractional parts
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = Math.Min(x0 + 1, sourceAccessor.Width - 1);
            int y1 = Math.Min(y0 + 1, sourceAccessor.Height - 1);

            double fx = x - x0;
            double fy = y - y0;

            // Sample the four surrounding pixels
            var row0 = sourceAccessor.GetRowSpan(y0);
            var row1 = sourceAccessor.GetRowSpan(y1);

            var p00 = row0[x0];
            var p10 = row0[x1];
            var p01 = row1[x0];
            var p11 = row1[x1];

            // Bilinear interpolation
            var r = (byte)Math.Round(
                (1 - fx) * (1 - fy) * p00.R +
                fx * (1 - fy) * p10.R +
                (1 - fx) * fy * p01.R +
                fx * fy * p11.R);

            var g = (byte)Math.Round(
                (1 - fx) * (1 - fy) * p00.G +
                fx * (1 - fy) * p10.G +
                (1 - fx) * fy * p01.G +
                fx * fy * p11.G);

            var b = (byte)Math.Round(
                (1 - fx) * (1 - fy) * p00.B +
                fx * (1 - fy) * p10.B +
                (1 - fx) * fy * p01.B +
                fx * fy * p11.B);

            return new Rgb24(r, g, b);
        }
    }
}

public static class ConvexHullExtensions
{
    public static RotatedRectangle? ToRotatedRectangle(this ConvexHull convexHull)
    {
        if (convexHull.Points.Count < 3)
            return null;

        double minArea = double.MaxValue;
        RotatedRectangle? bestRectangle = null;

        var points = convexHull.Points;

        int n = points.Count;

        // Try each edge as a potential side of the rectangle
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            var edge = (points[j].X - points[i].X, points[j].Y - points[i].Y);

            // Skip zero-length edges
            if (edge.Item1 == 0 && edge.Item2 == 0)
                continue;

            // Find the rectangle aligned with this edge
            var rectangle = FindRectangleAlignedWithEdge(points, edge);
            var area = rectangle?.Height * rectangle?.Width;

            if (area < minArea)
            {
                minArea = area.Value;
                bestRectangle = rectangle;
            }
        }

        return bestRectangle;

        static RotatedRectangle? FindRectangleAlignedWithEdge(IReadOnlyList<PointF> points, (double X, double Y) edge)
        {
            // 1. Compute edge unit vector and normal vector. These are the basis vectors for the rectangle
            // 2. Project points onto the basis vectors
            // 3. Find the minimum and maximum projections of points onto the basis vectors
            // 3. Transform from basic vector coordinates back to world coordinates

            var edgeLength = Math.Sqrt(edge.X * edge.X + edge.Y * edge.Y);
            var (ux, uy) = (edge.X / edgeLength, edge.Y / edgeLength);  // Edge unit vector
            var (nx, ny) = (-uy, ux);  // Edge normal vector

            // Compute minimum and maximum projections of points onto the basis vectors
            var minU = double.PositiveInfinity;
            var maxU = double.NegativeInfinity;
            var minN = double.PositiveInfinity;
            var maxN = double.NegativeInfinity;
            foreach (var point in points)
            {
                var projU = point.X * ux + point.Y * uy;
                var projN = point.X * nx + point.Y * ny;

                minU = Math.Min(minU, projU);
                maxU = Math.Max(maxU, projU);
                minN = Math.Min(minN, projN);
                maxN = Math.Max(maxN, projN);
            }

            var corner0 = (minU * ux + maxN * nx, minU * uy + maxN * ny);  // (minU, maxN)
            var corner1 = (maxU * ux + maxN * nx, maxU * uy + maxN * ny);  // (maxU, maxN)
            var corner2 = (maxU * ux + minN * nx, maxU * uy + minN * ny);  // (maxU, minN)
            var corner3 = (minU * ux + minN * nx, minU * uy + minN * ny);  // (minU, minN)

            List<PointF> corners = [corner0, corner1, corner2, corner3];

            // Check for collinearity
            for (int i = 1; i < corners.Count - 1; i++)
            {
                if (Math.Abs(CrossProductZ(corners[0], corners[i], corners[i + 1])) < 1e-8)
                    return null;
            }

            return new RotatedRectangle(corners);

            static double CrossProductZ(PointF a, PointF b, PointF c) =>
                (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }
    }
}
