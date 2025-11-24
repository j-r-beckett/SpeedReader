// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Numerics.Tensors;
using Microsoft.Extensions.DependencyInjection;
using Ocr.Algorithms;
using Ocr.Geometry;
using Ocr.InferenceEngine;
using Ocr.InferenceEngine.Engines;
using Ocr.Visualization;
using Resources.CharDict;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ocr;

public class TextRecognizer
{
    private readonly IInferenceEngine _inferenceEngine;
    private readonly EmbeddedCharDict _embeddedCharDict;
    private readonly int _inputWidth;
    private readonly int _inputHeight;

    public int InferenceEngineCapacity() => _inferenceEngine.CurrentMaxCapacity();

    public static TextRecognizer Factory(IServiceProvider serviceProvider, object? key)
    {
        var options = serviceProvider.GetRequiredService<RecognitionOptions>();
        var engine = serviceProvider.GetRequiredKeyedService<IInferenceEngine>(key);
        var dictionary = serviceProvider.GetRequiredService<EmbeddedCharDict>();
        return new TextRecognizer(engine, dictionary, options);
    }

    public TextRecognizer(IInferenceEngine inferenceEngine, EmbeddedCharDict embeddedCharDict, RecognitionOptions options)
    {
        _inferenceEngine = inferenceEngine;
        _embeddedCharDict = embeddedCharDict;
        _inputWidth = options.RecognitionInputWidth;
        _inputHeight = options.RecognitionInputHeight;
    }

    public List<(float[], int[])> Preprocess(List<BoundingBox> regions, Image<Rgb24> image)
    {
        var result = new List<(float[], int[])>();
        foreach (var region in regions)
        {
            var modelInput = PreprocessRegion(region, image, _inputHeight, _inputWidth);
            result.Add((modelInput, [3, _inputHeight, _inputWidth]));
        }

        return result;

        static float[] PreprocessRegion(BoundingBox region, Image<Rgb24> image, int height, int width)
        {
            using var textImg = region.RotatedRectangle.Crop(image);
            textImg.Mutate(x => x
                .Resize(new ResizeOptions
                {
                    Size = new Size(width, height),
                    Mode = ResizeMode.Pad,
                    Position = AnchorPositionMode.TopLeft
                }));
            // Normalize from [0, 255] to [-1, 1]
            Span<float> means = [127.5f, 127.5f, 127.5f];
            Span<float> stds = [127.5f, 127.5f, 127.5f];
            return textImg.ToNormalizedChwTensor(new Rectangle(0, 0, width, height), means, stds);
        }
    }

    public List<(string Text, double Confidence)> Postprocess((float[], int[])[] inferenceOutput)
    {
#if DEBUG
        var shape = inferenceOutput[0].Item2;
        Debug.Assert(inferenceOutput.Select(item => item.Item2).All(item => item.SequenceEqual(shape)));
        Debug.Assert(shape.Length == 2);
        Debug.Assert(shape[1] == _embeddedCharDict.Count);
#endif

        return inferenceOutput
            .Select(item => item.Item1.GreedyCTCDecode(_embeddedCharDict))
            .Select(item => (item.Text.Trim(), confidence: item.Confidence))
            .ToList();
    }

    // Override for testing only
    public virtual async Task<List<(string Text, double Confidence)>> Recognize(List<BoundingBox> regions, Image<Rgb24> image, VizBuilder vizBuilder)
    {
        var modelInput = Preprocess(regions, image);
        var inferenceOutput = await RunInference(modelInput);
        var results = Postprocess(inferenceOutput);

        // Add text items to visualization
        var textItems = new List<(string Text, double Confidence, List<(double X, double Y)> ORectangle)>();
        for (int i = 0; i < results.Count; i++)
        {
            var (text, confidence) = results[i];
            var rect = regions[i].RotatedRectangle;
            var corners = rect.Corners().Points.Select(p => (p.X, p.Y)).ToList();
            textItems.Add((text, confidence, corners));
        }
        vizBuilder.AddTextItems(textItems);

        return results;
    }

    public virtual async Task<(float[], int[])[]> RunInference(List<(float[], int[])> inputs)
    {
        List<Task<(float[], int[])>> inferenceTasks = [];
        foreach (var (data, shape) in inputs)
        {
            inferenceTasks.Add(await _inferenceEngine.Run(data, shape));
        }
        return await Task.WhenAll(inferenceTasks);
    }
}
