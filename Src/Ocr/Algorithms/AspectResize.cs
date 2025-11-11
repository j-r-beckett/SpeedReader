// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ocr.Algorithms;

public static class AspectResizeExtensions
{
    public static Image<Rgb24> HardAspectResize(this Image<Rgb24> src, Size targetSize)
    {
        // Calculate aspect-ratio-preserving scale
        var scaleX = (double)targetSize.Width / src.Width;
        var scaleY = (double)targetSize.Height / src.Height;
        var scale = Math.Min(scaleX, scaleY);

        // Calculate width and height of result rectangle
        var rectWidth = (int)Math.Round(src.Width * scale);
        var rectHeight = (int)Math.Round(src.Height * scale);

        return src.Clone(x => x
            .Resize(new ResizeOptions
            {
                Size = targetSize,
                Mode = ResizeMode.Manual,  // Use TargetRectangle to specify position and size
                TargetRectangle = new Rectangle(0, 0, rectWidth, rectHeight)  // Position at top left
            }));
    }
}


