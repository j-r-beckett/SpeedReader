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

    public DBNetBenchmark(int numThreads, int intraOpNumThreads, ModelPrecision quantization)
    {
        var config = new SessionOptions { IntraOpNumThreads = intraOpNumThreads };
        var dbnetSession = new ModelProvider().GetSession(Model.DbNet18, quantization, config);
        var modelRunner = new CpuModelRunner(dbnetSession, numThreads);
        _detector = new TextDetector(modelRunner);
        _modelRunner = modelRunner;
    }

    public async Task RunBenchmark(Image<Rgb24> input)
    {
        var modelInput = _detector.Preprocess(input, new VizBuilder());

        var inferenceTasks = Channel.CreateUnbounded<Task>();
        int numPendingInferenceTasks = 0;
        int numCompletedInferenceTasks = 0;

        var warmupPeriod = TimeSpan.FromSeconds(2);
        var testPeriod = TimeSpan.FromSeconds(10);

        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; stopwatch.Elapsed < testPeriod + warmupPeriod; i++)
        {
            Interlocked.Increment(ref numPendingInferenceTasks);
            var inferenceTask = await _modelRunner.Run(modelInput[i % modelInput.Count].Data, modelInput[i % modelInput.Count].Shape);
            var continueWith = inferenceTask.ContinueWith(t =>
            {
                if (t.IsFaulted) throw t.Exception;
                Interlocked.Decrement(ref numPendingInferenceTasks);
                if (stopwatch.Elapsed > warmupPeriod)
                {
                    Interlocked.Increment(ref numCompletedInferenceTasks);
                    if (numPendingInferenceTasks == 0 && stopwatch.Elapsed < testPeriod + warmupPeriod)
                    {
                        stopwatch.Stop();
                    }
                }
            });
            inferenceTasks.Writer.TryWrite(continueWith);
        }

        inferenceTasks.Writer.Complete();

        // Make sure we didn't throw any exceptions
        await foreach (var t in inferenceTasks.Reader.ReadAllAsync()) await t;

        var observedTestPeriod = stopwatch.Elapsed - warmupPeriod;

        Console.WriteLine($"Completed {numCompletedInferenceTasks} inference tasks in {observedTestPeriod.TotalSeconds:F2} sec");
    }
}
