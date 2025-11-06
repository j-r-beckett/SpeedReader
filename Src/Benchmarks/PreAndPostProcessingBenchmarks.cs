// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using BenchmarkDotNet.Attributes;
using Core;
using Ocr;
using Ocr.Geometry;
using Ocr.Inference;
using Ocr.Visualization;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Benchmarks;

public class DetectionPrePostBenchmark
{
    private TextDetector _detector = null!;
    private readonly Image<Rgb24> _input;
    private (float[], int[])[] _cachedInference = null!;

    public DetectionPrePostBenchmark()
    {
        var width = int.Parse(Environment.GetEnvironmentVariable("BENCHMARK_INPUT_WIDTH") ?? throw new InvalidOperationException("BENCHMARK_INPUT_WIDTH must be set"));
        var height = int.Parse(Environment.GetEnvironmentVariable("BENCHMARK_INPUT_HEIGHT") ?? throw new InvalidOperationException("BENCHMARK_INPUT_HEIGHT must be set"));
        var densityStr = Environment.GetEnvironmentVariable("BENCHMARK_DENSITY") ?? "high";
        var density = densityStr.ToLower() switch
        {
            "low" => Density.Low,
            "high" => Density.High,
            _ => throw new InvalidOperationException($"Invalid BENCHMARK_DENSITY value: {densityStr}. Must be 'low' or 'high'.")
        };
        _input = InputGenerator.GenerateInput(width, height, density);
    }

    [GlobalSetup]
    public async Task Setup()
    {
        var dbnetSession = new ModelProvider().GetSession(Model.DbNet18, ModelPrecision.INT8);
        var dbnetRunner = new CachingModelRunner(dbnetSession);
        _detector = new TextDetector(dbnetRunner);
        var modelInput = _detector.Preprocess(_input, _detector.Tile(_input), new VizBuilder());
        _cachedInference = await _detector.RunInference(modelInput);
    }

    [Benchmark]
    public void Run() => RunInternal();

    internal void RunInternal()
    {
        var tiling = _detector.Tile(_input);
        _detector.Preprocess(_input, tiling, new VizBuilder());
        _detector.Postprocess(_cachedInference, tiling, _input, new VizBuilder());
    }
}

public class RecognitionPrePostBenchmark
{
    private TextRecognizer _recognizer = null!;
    private readonly Image<Rgb24> _input;
    private List<BoundingBox> _detections = null!;
    private (float[], int[])[] _cachedInference = null!;

    public RecognitionPrePostBenchmark()
    {
        var width = int.Parse(Environment.GetEnvironmentVariable("BENCHMARK_INPUT_WIDTH") ?? throw new InvalidOperationException("BENCHMARK_INPUT_WIDTH must be set"));
        var height = int.Parse(Environment.GetEnvironmentVariable("BENCHMARK_INPUT_HEIGHT") ?? throw new InvalidOperationException("BENCHMARK_INPUT_HEIGHT must be set"));
        var densityStr = Environment.GetEnvironmentVariable("BENCHMARK_DENSITY") ?? "high";
        var density = densityStr.ToLower() switch
        {
            "low" => Density.Low,
            "high" => Density.High,
            _ => throw new InvalidOperationException($"Invalid BENCHMARK_DENSITY value: {densityStr}. Must be 'low' or 'high'.")
        };
        _input = InputGenerator.GenerateInput(width, height, density);
    }

    [GlobalSetup]
    public async Task Setup()
    {
        var dbnetSession = new ModelProvider().GetSession(Model.DbNet18, ModelPrecision.INT8);
        var dbnetRunner = new CpuModelRunner(dbnetSession, 1);
        var detector = new TextDetector(dbnetRunner);
        var tiling = detector.Tile(_input);
        var modelInput = detector.Preprocess(_input, tiling, new VizBuilder());
        var detectionInference = await detector.RunInference(modelInput);
        _detections = detector.Postprocess(detectionInference, tiling, _input, new VizBuilder());
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
