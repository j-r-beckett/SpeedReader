// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Numerics.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental.Detection;

public static class PixelsToFloatsExtensions
{
    public static float[] ToTensor(this Image<Rgb24> image, nint[] shape, float padding = 0)
    {
        var height = shape[0];
        var width = shape[1];
        var channels = shape[2];

        var result = new float[width * height * 3];

        result.AsSpan().Fill(padding);

        if (!image.DangerousTryGetSinglePixelMemory(out var imageData))
        {
            throw new NonContiguousImageException("Image memory is not contiguous");
        }

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var pixelRow = accessor.GetRowSpan(y);
                for (var x = 0; x < pixelRow.Length; x++)
                {
                    var pixel = imageData.Span[y * image.Width + x];
                    var destIndex = (y * width + x) * channels;
                    result[destIndex] = pixel.R;
                    result[destIndex + 1] = pixel.G;
                    result[destIndex + 2] = pixel.B;
                }
            }
        });

        return result;
    }
}

public class NonContiguousImageException : InvalidOperationException
{
    public NonContiguousImageException(string message) : base(message) { }
}
