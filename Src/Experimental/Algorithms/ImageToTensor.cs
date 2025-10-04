// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental.Algorithms;

public static class PixelsToFloatsExtensions
{
    public static float[] ToNormalizedChwTensor(this Image<Rgb24> image, int height, int width, float mean, float std)
    {
        Span<float> means = [mean, mean, mean];
        Span<float> stds = [std, std, std];
        return image.ToNormalizedChwTensor(new Rectangle(0, 0, image.Width, image.Height), height, width, means, stds);
    }

    public static float[] ToNormalizedChwTensor(this Image<Rgb24> image, Rectangle rect, int height, int width, ReadOnlySpan<float> means, ReadOnlySpan<float> stds)
    {
        if (means.Length != 3 || stds.Length != 3)
            throw new ArgumentException("means and stds must have length 3 (R, G, B)");

        var result = new float[3 * height * width];

        // Copy to local variables to avoid capturing spans in lambda
        var meanR = means[0];
        var meanG = means[1];
        var meanB = means[2];
        var stdR = stds[0];
        var stdG = stds[1];
        var stdB = stds[2];

        image.ProcessPixelRows(accessor =>
        {
            var maxH = Math.Min(rect.Height, height);
            var maxW = Math.Min(rect.Width, width);

            for (var h = 0; h < maxH; h++)
            {
                var row = accessor.GetRowSpan(rect.Top + h);
                for (var w = 0; w < maxW; w++)
                {
                    var pixel = row[rect.Left + w];
                    var rIndex = 0 * height * width + h * width + w;
                    var gIndex = 1 * height * width + h * width + w;
                    var bIndex = 2 * height * width + h * width + w;

                    result[rIndex] = (pixel.R - meanR) / stdR;
                    result[gIndex] = (pixel.G - meanG) / stdG;
                    result[bIndex] = (pixel.B - meanB) / stdB;
                }
            }
        });

        return result;
    }
}
