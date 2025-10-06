// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Threading.Channels;
using Core;
using Experimental;
using Experimental.Inference;
using Experimental.Visualization;
using Microsoft.ML.OnnxRuntime;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Benchmarks;

public class DBNetBenchmark
{
    private readonly TextDetector _detector;
    private readonly ModelRunner _modelRunner;
    private readonly int _testPeriodSeconds;

    public DBNetBenchmark(int numThreads, int intraOpNumThreads, ModelPrecision quantization, int testPeriodSeconds)
    {
        var config = new SessionOptions { IntraOpNumThreads = intraOpNumThreads };
        var dbnetSession = new ModelProvider().GetSession(Model.DbNet18, quantization, config);
        var modelRunner = new CpuModelRunner(dbnetSession, numThreads);
        _detector = new TextDetector(modelRunner);
        _modelRunner = modelRunner;
        _testPeriodSeconds = testPeriodSeconds;
    }

    public async Task<(int, TimeSpan, double)> RunBenchmark(Image<Rgb24> input)
    {
        var modelInput = _detector.Preprocess(input, new VizBuilder());

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
            var inferenceTask = await _modelRunner.Run(modelInput[i % modelInput.Count].Data, modelInput[i % modelInput.Count].Shape);
            var continueWith = inferenceTask.ContinueWith(t =>
            {
                if (t.IsFaulted) throw t.Exception;
                Interlocked.Decrement(ref numPendingInferenceTasks);
                if (stopwatch.Elapsed > warmupPeriod)
                {
                    Interlocked.Increment(ref numCompletedInferenceTasks);
                    if (numPendingInferenceTasks == 0 && actualCompletionTime == null)
                    {
                        actualCompletionTime = stopwatch.Elapsed;
                    }
                }
            });
            inferenceTasks.Writer.TryWrite(continueWith);
        }

        inferenceTasks.Writer.Complete();

        // Make sure we didn't throw any exceptions
        await foreach (var t in inferenceTasks.Reader.ReadAllAsync()) await t;

        var bandwidth = perfStarted ? perfBandwidth.Stop() : 0.0;
        var observedTestPeriod = (actualCompletionTime ?? stopwatch.Elapsed) - warmupPeriod;

        return (numCompletedInferenceTasks, observedTestPeriod, bandwidth);
    }
}
