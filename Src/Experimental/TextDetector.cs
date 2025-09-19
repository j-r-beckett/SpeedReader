// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Numerics.Tensors;
using CommunityToolkit.HighPerformance;
using Experimental.Inference;
using Ocr;
using Ocr.Algorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental;

public class TextDetector
{
    private readonly ModelRunner _modelRunner;

    public TextDetector(ModelRunner modelRunner) => _modelRunner = modelRunner;

    // Override for testing only
    public virtual async Task<List<TextBoundary>> Detect(Image<Rgb24> image, VizBuilder vizBuilder)
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

        var result = Postprocess(modelOutput, image, modelInputHeight, modelInputWidth);

        vizBuilder.AddAxisAlignedBBoxes(result.Select(r => r.AARectangle).ToList());
        vizBuilder.AddOrientedBBoxes(result.Select(r => r.ORectangle).ToList(), true);
        vizBuilder.AddPolygonBBoxes(result.Select(r => r.Polygon).ToList());

        return result;
    }

    private float[] Preprocess(Image<Rgb24> image, int height, int width)
    {
        float[] data = new float[height * width * 3];

        // Resize
        Resampling.AspectResizeInto(image, data, width, height);

        // Convert to CHW format
        TensorOps.NhwcToNchw(data, [height, width, 3]);

        // Apply ImageNet normalization
        float[] means = [123.675f, 116.28f, 103.53f];
        float[] stds = [58.395f, 57.12f, 57.375f];

        for (int channel = 0; channel < 3; channel++)
        {
            var tensor = Tensor.Create(data, channel * height * width, [height, width], default);

            // Subtract mean and divide by std in place
            Tensor.Subtract(tensor, means[channel], tensor);
            Tensor.Divide(tensor, stds[channel], tensor);
        }

        return data;
    }

    private List<TextBoundary> Postprocess(float[] modelOutput, Image<Rgb24> image, int height, int width)
    {
        Thresholding.BinarizeInPlace(modelOutput, 0.2f);
        var probabilityMapSpan = modelOutput.AsSpan().AsSpan2D(height, width);
        var boundaries = BoundaryTracing.FindBoundaries(probabilityMapSpan);
        List<TextBoundary> textBoundaries = [];

        foreach (var boundary in boundaries)
        {
            // Simplify
            var simplifiedPolygon = PolygonSimplification.DouglasPeucker(boundary);

            // Dilate
            var dilatedPolygon = Dilation.DilatePolygon(simplifiedPolygon.ToList());

            // Convert back to original coordinate system
            double scale = Math.Max((double)image.Width / probabilityMapSpan.Width, (double)image.Height / probabilityMapSpan.Height);
            Scale(dilatedPolygon, scale);

            // Clamp coordinates to image bounds
            ClampToImageBounds(dilatedPolygon, image.Height, image.Width);

            if (dilatedPolygon.Count >= 4)
            {
                textBoundaries.Add(TextBoundary.Create(dilatedPolygon));
            }
        }

        return textBoundaries;

        void Scale(List<(int X, int Y)> polygon, double scale)
        {
            for (int i = 0; i < polygon.Count; i++)
            {
                int originalX = (int)Math.Round(polygon[i].X * scale);
                int originalY = (int)Math.Round(polygon[i].Y * scale);
                polygon[i] = (originalX, originalY);
            }
        }

        void ClampToImageBounds(List<(int X, int Y)> polygon, int imageHeight, int imageWidth)
        {
            for (int i = 0; i < polygon.Count; i++)
            {
                int clampedX = Math.Max(0, Math.Min(imageWidth - 1, polygon[i].X));
                int clampedY = Math.Max(0, Math.Min(imageHeight - 1, polygon[i].Y));
                polygon[i] = (clampedX, clampedY);
            }
        }
    }
}
