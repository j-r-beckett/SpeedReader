// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Experimental.Inference;
using Ocr;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental;

public class TextRecognizer
{
    private readonly ModelRunner _modelRunner;

    public TextRecognizer(ModelRunner modelRunner) => _modelRunner = modelRunner;

    public async Task<(string, double)> Recognize(TextBoundary detection, Image<Rgb24> image)
    {
        var (modelInputHeight, modelInputWidth) = (48, 160);
        var modelInput = Preprocess(detection, image, modelInputHeight, modelInputWidth);
        var (modelOutput, shape) = await _modelRunner.Run(modelInput, [3, modelInputHeight, modelInputWidth]);
        Debug.Assert(shape.Length == 2);
        Debug.Assert(shape[0] == modelInputHeight);
        Debug.Assert(shape[1] == modelInputHeight);
        var result = Postprocess(modelOutput, modelInputWidth);
        return result;
    }

    private float[] Preprocess(TextBoundary detection, Image<Rgb24> image, int height, int width) => [];

    private (string, double) Postprocess(float[] modelOutput, int width)
    {
        Debug.Assert(width == CharacterDictionary.Count);
        return ("", 0);
    }
}
