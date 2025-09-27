// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Experimental.Geometry;
using Experimental.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental;

public record SpeedReaderResult
{
    public readonly Image<Rgb24> Image;
    public readonly List<(BoundingBox BBox, string Text, double Confidence)> Results;
    public readonly VizBuilder VizBuilder;

    public List<BoundingBox> Detections;
    public List<(string Text, double Confidence)> Recognitions;

    public SpeedReaderResult(Image<Rgb24> image, List<BoundingBox> detections, List<(string Text, double Confidence)> recognitions, VizBuilder vizBuilder)
    {
        Image = image;
        Detections = detections;
        Recognitions = recognitions;
        Results = Enumerable.Range(0, detections.Count)
            .Select(i => (detections[i], recognitions[i].Text, recognitions[i].Confidence))
            .ToList();
        VizBuilder = vizBuilder;
    }
}
