// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Channels;
using Core;
using Experimental;
using Experimental.Geometry;
using Experimental.Inference;
using Experimental.Visualization;
using Microsoft.ML.OnnxRuntime;
using Resources;

namespace Benchmarks;

internal class DummyModelRunner : ModelRunner
{
    public DummyModelRunner() : base(null!) { }
    public override Task<Task<(float[], int[])>> Run(float[] input, int[] shape) => throw new NotImplementedException();
    protected override ValueTask SubclassDisposeAsync() => ValueTask.CompletedTask;
}

public class InferenceBenchmark
{
    private readonly ModelRunner _modelRunner;
    private readonly int _testPeriodSeconds;
    private readonly InferenceSession _session;

    public InferenceBenchmark(Model model, int numThreads, int intraOpNumThreads, ModelPrecision quantization, int testPeriodSeconds)
    {
        var config = new SessionOptions { IntraOpNumThreads = intraOpNumThreads };
        var session = new ModelProvider().GetSession(model, quantization, config);
        _session = session;
        _modelRunner = new CpuModelRunner(session, numThreads);
        _testPeriodSeconds = testPeriodSeconds;
    }

    public void Cleanup()
    {
        _session.Dispose();
    }

    public async Task<(int, TimeSpan, double)> RunBenchmark(List<(float[] Data, int[] Shape)> modelInputs)
    {
        var inferenceTasks = Channel.CreateUnbounded<Task>();
        int numPendingInferenceTasks = 0;
        int numCompletedInferenceTasks = 0;

        var warmupPeriod = TimeSpan.FromSeconds(2);
        var testPeriod = TimeSpan.FromSeconds(_testPeriodSeconds);

        var stopwatch = Stopwatch.StartNew();
        TimeSpan? actualCompletionTime = null;
        using var perfBandwidth = new PerfMemoryBandwidth();
        bool perfStarted = false;

        for (int i = 0; stopwatch.Elapsed < testPeriod + warmupPeriod; i++)
        {
            if (!perfStarted && stopwatch.Elapsed > warmupPeriod)
            {
                perfBandwidth.Start();
                perfStarted = true;
            }

            Interlocked.Increment(ref numPendingInferenceTasks);
            var input = modelInputs[i % modelInputs.Count];
            var inferenceTask = await _modelRunner.Run(input.Data, input.Shape);
            var continueWith = inferenceTask.ContinueWith(t =>
            {
                if (t.IsFaulted) throw t.Exception;
                Interlocked.Decrement(ref numPendingInferenceTasks);
                if (stopwatch.Elapsed > warmupPeriod)
                {
                    Interlocked.Increment(ref numCompletedInferenceTasks);
                    actualCompletionTime = Max(actualCompletionTime ?? stopwatch.Elapsed, stopwatch.Elapsed);
                }
            });
            inferenceTasks.Writer.TryWrite(continueWith);
        }

        inferenceTasks.Writer.Complete();

        await foreach (var t in inferenceTasks.Reader.ReadAllAsync()) await t;

        var bandwidth = perfStarted ? perfBandwidth.Stop() : 0.0;
        var observedTestPeriod = (actualCompletionTime ?? stopwatch.Elapsed) - warmupPeriod;

        return (numCompletedInferenceTasks, observedTestPeriod, bandwidth);

        TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
    }
}

public static class DBNetBenchmarkHelper
{
    public static List<(float[] Data, int[] Shape)> GenerateInput(int width, int height, Density density)
    {
        var image = InputGenerator.GenerateInput(width, height, density);
        var detector = new TextDetector(new DummyModelRunner());
        return detector.Preprocess(image, new VizBuilder());
    }
}

public static class SVTRv2BenchmarkHelper
{
    public static List<(float[] Data, int[] Shape)> GenerateInput()
    {
        using var image = InputGenerator.GenerateInput(640, 640, Density.Low);

        var bbox = BoundingBox.Create(new Polygon
        {
            Points = new List<Point> { (100, 100), (200, 100), (200, 150), (100, 150) }.ToImmutableList()
        })!;

        var recognizer = new TextRecognizer(new DummyModelRunner());
        return recognizer.Preprocess([bbox, bbox, bbox, bbox], image);
    }
}
