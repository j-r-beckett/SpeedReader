// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Geometry;

public static partial class RotatedRectangleExtensions
{
    public static Image<Rgb24> Crop(this Image<Rgb24> image, RotatedRectangle rotatedRectangle)
    {
        var corners = rotatedRectangle.Corners();

        var topLeft = corners[0];
        var topRight = corners[1];
        var bottomLeft = corners[3];

        var outputWidth = (int)Math.Ceiling(rotatedRectangle.Width);
        var outputHeight = (int)Math.Ceiling(rotatedRectangle.Height);

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
    }

    private static Rgb24 BilinearSample(PixelAccessor<Rgb24> sourceAccessor, double x, double y)
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
