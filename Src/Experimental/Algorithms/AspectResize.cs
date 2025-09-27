// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Experimental.Algorithms;

public static class AspectResizeExtensions
{
    // The src image is scaled, maintaining aspect ratio, until one of its dimensions equals
    // the corresponding target dimension. The resulting image w x h will have w == width and h <= height,
    // or h == height and w <= width.
    public static Image<Rgb24> SoftAspectResize(this Image<Rgb24> src, int width, int height)
    {
        var config = Configuration.Default.Clone();
        config.PreferContiguousImageBuffers = true;

        return src.Clone(config, x => x
            .Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Max
            }));
    }

    // Scales the image to the target size, maintaining aspect ratio. Pads with black as necessary.
    // Returns an image of size exactly targetSize.
    public static Image<Rgb24> HardAspectResize(this Image<Rgb24> src, Size targetSize)
    {
        var config = Configuration.Default.Clone();
        config.PreferContiguousImageBuffers = true;

        // Calculate aspect-ratio-preserving scale
        var scaleX = (double)targetSize.Width / src.Width;
        var scaleY = (double)targetSize.Height / src.Height;
        var scale = Math.Min(scaleX, scaleY);

        // Calculate width and height of result rectangle
        var rectWidth = (int)Math.Round(src.Width * scale);
        var rectHeight = (int)Math.Round(src.Height * scale);

        return src.Clone(config, x => x
            .Resize(new ResizeOptions
            {
                Size = targetSize,
                Mode = ResizeMode.Manual,
                TargetRectangle = new Rectangle(0, 0, rectWidth, rectHeight)  // Position at top left
            }));
    }
}


