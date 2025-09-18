// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Numerics.Tensors;
using Experimental.Inference;
using Ocr.Algorithms;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental;

public class TextRecognizer
{
    private readonly ModelRunner _modelRunner;
    private readonly VizBuilder _vizBuilder;

    public TextRecognizer(ModelRunner modelRunner, VizBuilder vizBuilder)
    {
        _modelRunner = modelRunner;
        _vizBuilder = vizBuilder;
    }

    // Override for testing only
    public virtual async Task<(string Text, double Confidence)> Recognize(List<(double X, double Y)> oRectangle, Image<Rgb24> image)
    {
        var (modelInputHeight, modelInputWidth) = (48, 160);
        var modelInput = Preprocess(oRectangle, image, modelInputHeight, modelInputWidth);
        var (modelOutput, shape) = await _modelRunner.Run(modelInput, [3, modelInputHeight, modelInputWidth]);
        Debug.Assert(shape.Length == 2);
        Debug.Assert(shape[1] == CharacterDictionary.Count);
        var (text, confidence) = Postprocess(modelOutput);
        _vizBuilder.AddTextItems([(text, confidence, oRectangle)]);
        return (text, confidence);
    }

    private float[] Preprocess(List<(double X, double Y)> oRectangle, Image<Rgb24> image, int height, int width)
    {
        float[] data = new float[height * width * 3];

        using var croppedImage = ImageCropping.CropOriented(image, oRectangle);

        Resampling.AspectResizeInto(croppedImage, data, width, height, 127.5f);

        // Convert to CHW format in place
        TensorOps.NhwcToNchw(data, [height, width, 3]);

        // Normalize: [0,255] -> [-1,1]
        for (int channel = 0; channel < 3; channel++)
        {
            int channelOffset = channel * height * width;
            var channelTensor = Tensor.Create(data, channelOffset, [height, width], default);

            Tensor.Divide(channelTensor, 127.5f, channelTensor);
            Tensor.Subtract(channelTensor, 1.0f, channelTensor);
        }

        return data;
    }

    private (string, double) Postprocess(float[] modelOutput)
        => CTC.DecodeSingleSequence(modelOutput, CharacterDictionary.Count);
}
