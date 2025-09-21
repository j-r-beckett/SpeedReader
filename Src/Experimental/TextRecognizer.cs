// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Numerics.Tensors;
using Experimental.BoundingBoxes;
using Experimental.Detection;
using Experimental.Inference;
using Ocr.Algorithms;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental;

public class TextRecognizer
{
    private readonly ModelRunner _modelRunner;

    public TextRecognizer(ModelRunner modelRunner) => _modelRunner = modelRunner;

    // Override for testing only
    public virtual async Task<(string Text, double Confidence)> Recognize(RotatedRectangle region, Image<Rgb24> image, VizBuilder vizBuilder)
    {
        var (modelInputHeight, modelInputWidth) = (48, 160);
        var oRectangle = region.Corners().Select(p => (p.X, p.Y)).ToList();
        var modelInput = Preprocess(region, image, modelInputHeight, modelInputWidth);
        var (modelOutput, shape) = await _modelRunner.Run(modelInput, [3, modelInputHeight, modelInputWidth]);
        Debug.Assert(shape.Length == 2);
        Debug.Assert(shape[1] == CharacterDictionary.Count);
        var (text, confidence) = Postprocess(modelOutput);
        vizBuilder.AddTextItems([(text, confidence, oRectangle)]);
        return (text, confidence);
    }

    private float[] Preprocess(RotatedRectangle region, Image<Rgb24> image, int height, int width)
    {
        using var resized = image.Crop(region).AspectResizeInPlace(width, height);

        var tensor = resized.ToTensor([height, width, 3], 127.5f);
        tensor.NhwcToNchwInPlace([height, width, 3]);

        // Normalize
        for (int channel = 0; channel < 3; channel++)
        {
            int channelOffset = channel * height * width;
            var channelTensor = Tensor.Create(tensor, channelOffset, [height, width], default);

            Tensor.Divide(channelTensor, 127.5f, channelTensor);  // [0,255] -> [0,2]
            Tensor.Subtract(channelTensor, 1.0f, channelTensor);  // [0,2] -> [-1,1]
        }

        return tensor;
    }

    private (string, double) Postprocess(float[] modelOutput)
        => CTC.DecodeSingleSequence(modelOutput, CharacterDictionary.Count);
}
