// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Visualization;

public class VizData
{
    public Image<L8>? ProbabilityMap { get; set; }
    public List<TextBoundary> FilteredTextBoxes { get; set; } = [];
}
