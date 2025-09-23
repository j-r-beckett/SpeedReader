// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Experimental.Detection;

public static class AspectResizeExtensions
{
    // Scales image as large as possible (preserves aspect ratio) while fitting within destination bounds, then places
    // at top-left corner of dest.
    public static Image<Rgb24> AspectResize(this Image<Rgb24> src, int destWidth, int destHeight)
    {
        var scale = Math.Min((double)destWidth / src.Width, (double)destHeight / src.Height);
        var targetWidth = (int)Math.Round(src.Width * scale);
        var targetHeight = (int)Math.Round(src.Height * scale);

        var config = Configuration.Default.Clone();
        config.PreferContiguousImageBuffers = true;

        return src.Clone(config, x => x
            .Resize(new ResizeOptions
            {
                Size = new Size(targetWidth, targetHeight),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Bicubic,
            }));
    }
}


