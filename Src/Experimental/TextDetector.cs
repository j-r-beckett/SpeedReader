// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Experimental.Inference;
using Ocr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental;

public class TextDetector
{
    private readonly ModelRunner _modelRunner;

    public TextDetector(ModelRunner modelRunner) => _modelRunner = modelRunner;

    public async Task<List<TextBoundary>> Detect(Image<Rgb24> image)
    {
        var modelInput = Preprocess(image);
        var modelOutput = await _modelRunner.Run(modelInput);
        var result = Postprocess(modelOutput, image);
        return result;
    }

    private float[] Preprocess(Image<Rgb24> image) => [];

    private List<TextBoundary> Postprocess(float[] modelOutput, Image<Rgb24> image) => [];
}
