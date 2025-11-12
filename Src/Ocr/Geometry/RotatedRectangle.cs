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

        if (Math.Abs(angle - Math.PI / 2) < 0.00001)
        {
            // When angle is pi/2 (vertical edge going up), we need to use the top-right corner
            // of the original rectangle as the starting point to ensure proper orientation
            topLeft = new PointF
            {
                X = Math.Max(corners[0].X, Math.Max(corners[1].X, Math.Max(corners[2].X, corners[3].X))),
                Y = Math.Min(corners[0].Y, Math.Min(corners[1].Y, Math.Min(corners[2].Y, corners[3].Y)))
            };
        }
        else if (Math.Abs(angle + Math.PI / 2) < 0.00001)
        {
            // When angle is -pi/2 (vertical edge going down), we need to use the bottom-left corner
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

    public AxisAlignedRectangle ToAxisAlignedRectangle()
    {
        var points = Corners().Points;

        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        foreach (var point in points)
        {
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
        }

        // Ensure rectangle completely encloses points
        return new AxisAlignedRectangle
        {
            X = (int)Math.Floor(minX),
            Y = (int)Math.Floor(minY),
            Width = (int)Math.Ceiling(maxX) - (int)Math.Floor(minX),
            Height = (int)Math.Ceiling(maxY) - (int)Math.Floor(minY)
        };
    }
}
