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


    // The src image is scaled, maintaining aspect ratio, to fit within the target bounds.
    // Uses Manual mode so the transformation can be perfectly reversed with UndoHardAspectResize.
    // The resulting image will always be exactly targetSize width x height.
    public static Image<Rgb24> HardAspectResize(this Image<Rgb24> src, Size targetSize)
    {
        var config = Configuration.Default.Clone();
        config.PreferContiguousImageBuffers = true;

        var contentRect = CalculateContentRect(src.Size, targetSize);

        return src.Clone(config, x => x
            .Resize(new ResizeOptions
            {
                Size = targetSize,  // result outside contentRect will be filled Black
                Mode = ResizeMode.Manual,
                TargetRectangle = contentRect  // Top-left of result
            }));
    }

    // Reverses a HardAspectResize operation
    public static Image<Rgb24> UndoHardAspectResize(Image<Rgb24> originalImage, Image<Rgb24> resizedImage)
    {
        var config = Configuration.Default.Clone();
        config.PreferContiguousImageBuffers = true;

        var contentRect = CalculateContentRect(originalImage.Size, resizedImage.Size);

        return resizedImage.Clone(config, x => x
            .Crop(contentRect)
            .Resize(originalImage.Size));
    }

    // Calculates where content is positioned in a HardAspectResize operation
    private static Rectangle CalculateContentRect(Size originalSize, Size targetSize)
    {
        // Calculate aspect-ratio-preserving scale
        var scaleX = (double)targetSize.Width / originalSize.Width;
        var scaleY = (double)targetSize.Height / originalSize.Height;
        var scale = Math.Min(scaleX, scaleY);

        // Calculate content dimensions and position
        var contentWidth = (int)Math.Round(originalSize.Width * scale);
        var contentHeight = (int)Math.Round(originalSize.Height * scale);

        return new Rectangle(0, 0, contentWidth, contentHeight);
    }
}


