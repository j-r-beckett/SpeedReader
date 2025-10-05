// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Numerics.Tensors;
using Experimental.Algorithms;
using Experimental.Geometry;
using Experimental.Inference;
using Experimental.Visualization;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Experimental;

public class TextRecognizer
{
    private readonly ModelRunner _modelRunner;

    public TextRecognizer(ModelRunner modelRunner) => _modelRunner = modelRunner;

    public List<(float[], int[])> Preprocess(List<BoundingBox> regions, Image<Rgb24> image)
    {
        var result = new List<(float[], int[])>();
        foreach (var region in regions)
        {
            var (modelInputHeight, modelInputWidth) = (48, 160);
            var modelInput = PreprocessRegion(region, image, modelInputHeight, modelInputWidth);
            result.Add((modelInput, [3, modelInputHeight, modelInputWidth]));
        }

        return result;

        static float[] PreprocessRegion(BoundingBox region, Image<Rgb24> image, int height, int width)
        {
            using var textImg = image.Crop(region.RotatedRectangle);
            textImg.Mutate(x => x
                .Resize(new ResizeOptions
                {
                    Size = new Size(width, height),
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Triangle
                }));
            return textImg.ToNormalizedChwTensor(height, width, 127.5f, 127.5f);
        }
    }

    public List<(string Text, double Confidence)> Postprocess((float[], int[])[] inferenceOutput)
    {
#if DEBUG
        var shape = inferenceOutput[0].Item2;
        Debug.Assert(inferenceOutput.Select(item => item.Item2).All(item => item.SequenceEqual(shape)));
        Debug.Assert(shape.Length == 2);
        Debug.Assert(shape[1] == CharacterDictionary.Count);
#endif

        return inferenceOutput
            .Select(item => item.Item1.GreedyCTCDecode())
            .Select(item => (item.Text.Trim(), confidence: item.Confidence))
            .ToList();
    }

    // Override for testing only
    public virtual async Task<List<(string Text, double Confidence)>> Recognize(List<BoundingBox> regions, Image<Rgb24> image, VizBuilder vizBuilder)
    {
        var modelInput = Preprocess(regions, image);
        var inferenceOutput = await RunInference(modelInput);
        return Postprocess(inferenceOutput);

    }

    public virtual async Task<(float[], int[])[]> RunInference(List<(float[], int[])> inputs)
    {
        var inferenceTasks = await Task.WhenAll(inputs.Select(t => _modelRunner.Run(t.Item1, t.Item2)));
        return await Task.WhenAll(inferenceTasks);
    }
}
