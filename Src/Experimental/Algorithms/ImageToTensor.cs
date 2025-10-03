// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental.Algorithms;

public static class PixelsToFloatsExtensions
{
    public static float[] ToTensor(this Image<Rgb24> image, nint[] shape, float padding = 0)
    {
        var height = shape[0];
        var width = shape[1];
        var channels = shape[2];

        var result = new float[width * height * 3];

        image.ProcessPixelRows(rowAccessor =>
        {
            for (var y = 0; y < rowAccessor.Height; y++)
            {
                var row = rowAccessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    var destIndex = (y * width + x) * channels;
                    var pixel = row[x];
                    result[destIndex] = pixel.R;
                    result[destIndex + 1] = pixel.G;
                    result[destIndex + 2] = pixel.B;
                }
            }
        });

        for (int y = image.Height + 1; y < height; y++)
        {
            for (int x = image.Width * (int)channels + 1; x < width; x++)
            {
                result[y * width * channels + x] = padding;
            }
        }

        return result;
    }
}

public class NonContiguousImageException : InvalidOperationException
{
    public NonContiguousImageException(string message) : base(message) { }
}
