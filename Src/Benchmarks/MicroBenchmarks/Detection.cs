// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Core;
using Experimental;
using Experimental.Visualization;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Benchmarks.MicroBenchmarks;

public class Detection
{
    private readonly TextDetector _detector;
    private readonly Image<Rgb24> _input;
    private readonly (float[], int[])[] _cachedInference;

    public Detection()
    {
        _input = InputGenerator.GenerateImages(640, 640, 32, CancellationToken.None).FirstAsync().GetAwaiter().GetResult();
        var dbnetSession = new ModelProvider().GetSession(Model.DbNet18, ModelPrecision.INT8);
        var dbnetRunner = new CachingModelRunner(dbnetSession);
        _detector = new TextDetector(dbnetRunner);
        var modelInput = _detector.Preprocess(_input, new VizBuilder());
        _cachedInference = _detector.RunInference(modelInput).GetAwaiter().GetResult();
    }

    [Benchmark]
    public void Detect()
    {
        _detector.Preprocess(_input, new VizBuilder());
        _detector.Postprocess(_cachedInference, _input, new VizBuilder());
    }
}
