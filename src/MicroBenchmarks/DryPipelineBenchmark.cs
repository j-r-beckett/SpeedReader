// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SpeedReader.Ocr;
using SpeedReader.Ocr.Geometry;
using SpeedReader.Ocr.InferenceEngine;
using SpeedReader.Ocr.Visualization;

namespace SpeedReader.MicroBenchmarks;

[SimpleJob(RuntimeMoniker.Net10_0)]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
[MemoryDiagnoser]
public class DryPipelineBenchmark
{
    private Image<Rgb24> _image = null!;
    private TextDetector _detector = null!;
    private TextRecognizer _recognizer = null!;
    private TextDetector.Tiling _tiling = null!;
    private (float[] Data, int[] Shape)[] _cachedDetectionOutput = null!;
    private List<BoundingBox> _cachedBoundingBoxes = null!;
    private (float[] Data, int[] Shape)[] _cachedRecognitionOutput = null!;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        var expectedNumberOfTiles = 4;
        _image = InputGenerator.GenerateInput(1080, 720);

        var options = new OcrPipelineOptions
        {
            DetectionOptions = new DetectionOptions(),
            RecognitionOptions = new RecognitionOptions(),
            DetectionEngine = new CpuEngineConfig
            {
                Kernel = new OnnxInferenceKernelOptions(
                    model: Model.DbNet,
                    quantization: Quantization.Int8,
                    numIntraOpThreads: 1),
                Parallelism = 1
            },
            RecognitionEngine = new CpuEngineConfig
            {
                Kernel = new OnnxInferenceKernelOptions(
                    model: Model.Svtr,
                    quantization: Quantization.Fp32,
                    numIntraOpThreads: 1),
                Parallelism = 1
            }
        };

        var services = new ServiceCollection();
        services.AddOcrPipeline(options);
        var provider = services.BuildServiceProvider();
        _detector = provider.GetRequiredService<TextDetector>();
        _recognizer = provider.GetRequiredService<TextRecognizer>();

        // Run detection pipeline and cache outputs
        _tiling = _detector.Tile(_image);
        if (_tiling.Tiles.Count != expectedNumberOfTiles)
            throw new Exception($"Unexpected tile count. Expected {_tiling.Tiles.Count}, got {expectedNumberOfTiles}");
        var vizBuilder = new VizBuilder();
        var detectionPreprocessOutput = _detector.Preprocess(_image, _tiling, vizBuilder);
        _cachedDetectionOutput = await _detector.RunInference(detectionPreprocessOutput);
        _cachedBoundingBoxes = _detector.Postprocess(_cachedDetectionOutput, _tiling, _image, vizBuilder);

        // Run recognition pipeline and cache outputs
        var recognitionPreprocessOutput = _recognizer.Preprocess(_cachedBoundingBoxes, _image);
        _cachedRecognitionOutput = await _recognizer.RunInference(recognitionPreprocessOutput);
    }

    [Benchmark]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public List<(string Text, double Confidence)> PreprocessAndPostprocess()
    {
        var vizBuilder = new VizBuilder();

        // Detection pre/post processing
        _detector.Preprocess(_image, _tiling, vizBuilder);
        var boundingBoxes = _detector.Postprocess(_cachedDetectionOutput, _tiling, _image, vizBuilder);

        // Recognition pre/post processing
        _recognizer.Preprocess(boundingBoxes, _image);
        var results = _recognizer.Postprocess(_cachedRecognitionOutput);

        return results;
    }
}
