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


        if (!image.DangerousTryGetSinglePixelMemory(out var imageData))
        {
            throw new NonContiguousImageException("Image memory is not contiguous");
        }

        var result = new float[width * height * 3];

        var imageDataSpan = imageData.Span;

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var destIndex = (y * width + x) * channels;
                var pixel = x >= width || y >= height
                    ? new Rgb24(0, 0, 0)
                    : imageDataSpan[y * image.Width + x];
                result[destIndex] = pixel.R;
                result[destIndex + 1] = pixel.G;
                result[destIndex + 2] = pixel.B;
            }
        }

        return result;
    }
}

public class NonContiguousImageException : InvalidOperationException
{
    public NonContiguousImageException(string message) : base(message) { }
}
