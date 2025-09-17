// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Ocr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental;

public class TextRecognizer
{
    private readonly Func<float[], Task<float[]>> _runInference;

    public TextRecognizer(Func<float[], Task<float[]>> runInference) => _runInference = runInference;

    public async Task<(string, double)> Recognize(TextBoundary detection, Image<Rgb24> image)
    {
        var modelInput = Preprocess(image);
        var modelOutput = await _runInference(modelInput);
        var result = Postprocess(modelOutput);
        return result;
    }

    private float[] Preprocess(Image<Rgb24> image) => [];

    private (string, double) Postprocess(float[] modelOutput) => ("", 0);
}
