// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Numerics.Tensors;
using CommunityToolkit.HighPerformance;
using Experimental.BoundingBoxes;
using Experimental.Detection;
using Experimental.Inference;
using Ocr;
using Ocr.Algorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Point = Experimental.BoundingBoxes.Point;

namespace Experimental;

public class TextDetector
{
    private readonly ModelRunner _modelRunner;

    public TextDetector(ModelRunner modelRunner) => _modelRunner = modelRunner;

    // Override for testing only
    public virtual async Task<List<BoundingBox>> Detect(Image<Rgb24> image, VizBuilder vizBuilder)
    {
        vizBuilder.AddBaseImage(image);

        var (modelInputHeight, modelInputWidth) = (640, 640);
        var modelInput = Preprocess(image, modelInputHeight, modelInputWidth);
        var (modelOutput, shape) = await _modelRunner.Run(modelInput, [3, modelInputHeight, modelInputWidth]);
        Debug.Assert(shape.Length == 2);
        Debug.Assert(shape[0] == modelInputHeight);
        Debug.Assert(shape[1] == modelInputHeight);

        var probabilityMapSpan = modelOutput.AsSpan().AsSpan2D(modelInputHeight, modelInputWidth);
        vizBuilder.CreateAndAddProbabilityMap(probabilityMapSpan, image.Width, image.Height);

        var boundingBoxes = Postprocess(modelOutput, image, modelInputHeight, modelInputWidth);

        vizBuilder.AddBoundingBoxes(boundingBoxes);

        return boundingBoxes;
    }

    private float[] Preprocess(Image<Rgb24> image, int height, int width)
    {
        using var resized = image.Clone().AspectResizeInPlace(width, height);

        var tensor = resized.ToTensor([height, width, 3]);
        tensor.NhwcToNchwInPlace([height, width, 3]);

        // Apply ImageNet normalization
        float[] means = [123.675f, 116.28f, 103.53f];
        float[] stds = [58.395f, 57.12f, 57.375f];

        for (int channel = 0; channel < 3; channel++)
        {
            var channelTensor = Tensor.Create(tensor, channel * height * width, [height, width], default);

            // Subtract mean and divide by std in place
            Tensor.Subtract(channelTensor, means[channel], channelTensor);
            Tensor.Divide(channelTensor, stds[channel], channelTensor);
        }

        return tensor;
    }

    private List<BoundingBox> Postprocess(float[] modelOutput, Image<Rgb24> image, int height, int width)
    {
        modelOutput.BinarizeInPlace(0.2f);
        var probabilityMapSpan = modelOutput.AsSpan().AsSpan2D(height, width);
        var boundaries = BoundaryTracing.FindBoundaries(probabilityMapSpan)
            .Select(b => b.Select(p => (Point)p).ToList())
            .Select(points => new Polygon { Points = points });

        // Multiplying by this undoes the transformation from image coordinates to model coordinates that we did in preprocessing with AspectResizeInPlace and ToTensor
        var scale = Math.Max((double)image.Width / probabilityMapSpan.Width, (double)image.Height / probabilityMapSpan.Height);

        return boundaries
            .Select(BoundaryToBBox)
            .OfType<BoundingBox>()  // Filter out nulls
            .ToList();

        BoundingBox? BoundaryToBBox(Polygon boundary)
        {
            var polygon = boundary
                .Simplify()  // Remove redundant points
                .Dilate(1.5)  // Undo contraction baked into DBNet during training; 1.5 is a model-specific constant
                .Scale(scale)  // Convert from model coordinates to image coordinates
                .Clamp(image.Height - 1, image.Width - 1);  // Make sure we don't go out of bounds

            if (polygon.Points.Count <= 4)
                return null;  // Not enough points to define a polygon

            return new BoundingBox(polygon);  // Bounding box construction creates rotated rectangle and axis-aligned rectangle
        }
    }
}
