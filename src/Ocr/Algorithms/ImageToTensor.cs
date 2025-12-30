// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SpeedReader.Ocr.Algorithms;

public static class PixelsToFloatsExtensions
{
    public static float[] ToNormalizedChwTensor(this Image<Rgb24> image, Rectangle rect, ReadOnlySpan<float> means, ReadOnlySpan<float> stds)
    {
        if (means.Length != 3 || stds.Length != 3)
            throw new ArgumentException("means and stds must have length 3 (R, G, B)");

        var height = rect.Height;
        var width = rect.Width;
        var result = new float[3 * height * width];  // CHW, 3 channels (rgb), height, width

        // Copy to local variables b/c we can't use spans in the ProcessPixelRows lambda
        var meanR = means[0];
        var meanG = means[1];
        var meanB = means[2];
        var stdR = stds[0];
        var stdG = stds[1];
        var stdB = stds[2];

        image.ProcessPixelRows(accessor =>
        {
            for (var h = 0; h < height; h++)
            {
                var row = accessor.GetRowSpan(rect.Top + h);
                for (var w = 0; w < width; w++)
                {
                    // Pixel position is relative to rectangle
                    var pixel = row[rect.Left + w];

                    // Navigate to C -> H -> W tensor position
                    var rIndex = 0 * height * width + h * width + w;
                    var gIndex = 1 * height * width + h * width + w;
                    var bIndex = 2 * height * width + h * width + w;

                    // Copy from pixel to tensor, normalizing as we go
                    result[rIndex] = (pixel.R - meanR) / stdR;
                    result[gIndex] = (pixel.G - meanG) / stdG;
                    result[bIndex] = (pixel.B - meanB) / stdB;
                }
            }
        });

        return result;
    }
}
