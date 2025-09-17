// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Experimental.Inference;
using Ocr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental;

public class TextRecognizer
{
    private readonly ModelRunner _modelRunner;

    public TextRecognizer(ModelRunner modelRunner) => _modelRunner = modelRunner;

    public async Task<(string, double)> Recognize(TextBoundary detection, Image<Rgb24> image)
    {
        var modelInput = Preprocess(detection, image);
        var modelOutput = await _modelRunner.Run(modelInput);
        var result = Postprocess(modelOutput);
        return result;
    }

    private float[] Preprocess(TextBoundary detection, Image<Rgb24> image) => [];

    private (string, double) Postprocess(float[] modelOutput) => ("", 0);
}
