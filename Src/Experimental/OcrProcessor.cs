// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Experimental.Inference;
using Ocr;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental;

public class OcrProcessor : BaseProcessor<Image<Rgb24>, OcrResult>
{
    private readonly ModelRunner _dbnetRunner;
    private readonly ModelRunner _svtrRunner;

    public OcrProcessor(ModelRunner dbnetRunner, ModelRunner svtrRunner, int maxParallelism) : base(maxParallelism)
    {
        _dbnetRunner = dbnetRunner;
        _svtrRunner = svtrRunner;
    }

    protected override async Task<OcrResult> ProcessProtected(Image<Rgb24> image)
    {
        var detector = new TextDetector(DBNetInference);
        var recognizer = new TextRecognizer(SVTRv2Inference);

        var detections = await detector.Detect(image);
        var recognitionTasks = detections.Select(d => recognizer.Recognize(d, image)).ToList();
        var recognitions = await Task.WhenAll(recognitionTasks);
        return AssembleResults(detections, recognitions.ToList());
    }

    private OcrResult AssembleResults(List<TextBoundary> detections,
        List<(string Text, double Confidence)> recognitions) => new OcrResult();

    // Override for testing
    protected virtual Task<float[]> DBNetInference(float[] input) => RunOutside(() => _dbnetRunner.Run(input));

    // Override for testing
    protected virtual Task<float[]> SVTRv2Inference(float[] input) => RunOutside(() => _svtrRunner.Run(input));
}
