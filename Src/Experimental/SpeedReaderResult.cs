// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Ocr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental;

public record SpeedReaderResult
{
    public readonly Image<Rgb24> Image;
    public readonly List<(TextBoundary BBox, string Text, double Confidence)> Results;
    public readonly VizBuilder VizBuilder;

    public SpeedReaderResult(Image<Rgb24> image, List<TextBoundary> detections, List<(string Text, double Confidence)> recognitions, VizBuilder vizBuilder)
    {
        Image = image;
        Results = Enumerable.Range(0, detections.Count)
            .Select(i => (detections[i], recognitions[i].Text, recognitions[i].Confidence))
            .ToList();
        VizBuilder = vizBuilder;
    }
}
