// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Visualization;

public class VoidVizBuilder : VizBuilder
{
    public VoidVizBuilder(Image<Rgb24> sourceImage) : base(sourceImage)
    {
    }

    public override Image<Rgb24> Render()
    {
        throw new InvalidOperationException("VoidVizBuilder does not support rendering");
    }
}
