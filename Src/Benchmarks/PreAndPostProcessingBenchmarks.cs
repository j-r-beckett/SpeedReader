// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using BenchmarkDotNet.Attributes;
using Core;
using Experimental;
using Experimental.Geometry;
using Experimental.Inference;
using Experimental.Visualization;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Benchmarks;

public class DetectionPrePostBenchmark
{
    private TextDetector _detector = null!;
    private Image<Rgb24> _input = null!;
    private (float[], int[])[] _cachedInference = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _input = await InputGenerator.GenerateImages(640, 640, 32, CancellationToken.None).FirstAsync();
        var dbnetSession = new ModelProvider().GetSession(Model.DbNet18, ModelPrecision.INT8);
        var dbnetRunner = new CachingModelRunner(dbnetSession);
        _detector = new TextDetector(dbnetRunner);
        var modelInput = _detector.Preprocess(_input, new VizBuilder());
        _cachedInference = await _detector.RunInference(modelInput);
    }

    [Benchmark]
    public void Run() => RunInternal();

    internal void RunInternal()
    {
        _detector.Preprocess(_input, new VizBuilder());
        _detector.Postprocess(_cachedInference, _input, new VizBuilder());
    }
}

public class RecognitionPrePostBenchmark
{
    private TextRecognizer _recognizer = null!;
    private Image<Rgb24> _input = null!;
    private List<BoundingBox> _detections = null!;
    private (float[], int[])[] _cachedInference = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _input = await InputGenerator.GenerateImages(640, 640, 32, CancellationToken.None).FirstAsync();
        var dbnetSession = new ModelProvider().GetSession(Model.DbNet18, ModelPrecision.INT8);
        var dbnetRunner = new CpuModelRunner(dbnetSession, 1);
        var detector = new TextDetector(dbnetRunner);
        var modelInput = detector.Preprocess(_input, new VizBuilder());
        var detectionInference = await detector.RunInference(modelInput);
        _detections = detector.Postprocess(detectionInference, _input, new VizBuilder());
        var svtrSession = new ModelProvider().GetSession(Model.SVTRv2);
        var svtrRunner = new CpuModelRunner(svtrSession, 1);
        _recognizer = new TextRecognizer(svtrRunner);
        var recognitionModelInput = _recognizer.Preprocess(_detections, _input);
        _cachedInference = await _recognizer.RunInference(recognitionModelInput);
    }

    [Benchmark]
    public void Run() => RunInternal();

    internal void RunInternal()
    {
        _recognizer.Preprocess(_detections, _input);
        _recognizer.Postprocess(_cachedInference);
    }
}

public class DetectionAndRecognitionPrePostBenchmark
{
    private DetectionPrePostBenchmark _detectionBenchmark = null!;
    private RecognitionPrePostBenchmark _recognitionBenchmark = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _detectionBenchmark = new DetectionPrePostBenchmark();
        await _detectionBenchmark.Setup();
        _recognitionBenchmark = new RecognitionPrePostBenchmark();
        await _recognitionBenchmark.Setup();
    }

    [Benchmark]
    public void Run()
    {
        _detectionBenchmark.RunInternal();
        _recognitionBenchmark.RunInternal();
    }
}
