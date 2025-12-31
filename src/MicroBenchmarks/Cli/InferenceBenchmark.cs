// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using SpeedReader.Native;
using SpeedReader.Ocr.InferenceEngine;
using SpeedReader.Resources.Weights;

namespace SpeedReader.MicroBenchmarks.Cli;

public static class InferenceBenchmark
{
    public static void Run(Model model, double warmup, int intraThreads, int interThreads,
        int parallelism, double duration, int batchSize, bool profile)
    {
        if (parallelism == 1)
            RunSingleThreaded(model, warmup, intraThreads, interThreads, duration, batchSize, profile);
        else
            RunParallel(model, warmup, intraThreads, interThreads, parallelism, duration, batchSize, profile);
    }

    private static void RunSingleThreaded(Model model, double warmup, int intraThreads, int interThreads,
        double duration, int batchSize, bool profile)
    {
        var (inputShape, outputShape) = GetShapes(model, batchSize);

        var weights = model == Model.DbNet ? EmbeddedWeights.Dbnet_Int8 : EmbeddedWeights.Svtr_Fp32;
        var sessionOptions = new SessionOptions()
            .WithIntraOpThreads(intraThreads)
            .WithInterOpThreads(interThreads)
            .WithProfiling(profile);

        using var session = new InferenceSession(weights.Bytes, sessionOptions);

        var inputSize = inputShape.Aggregate(1, (a, b) => a * b);
        var outputSize = outputShape.Aggregate(1, (a, b) => a * b);
        var inputData = new float[inputSize];
        var outputData = new float[outputSize];

        var rng = new Random(42);
        for (var i = 0; i < inputData.Length; i++)
            inputData[i] = rng.NextSingle();

        var input = OrtValue.Create(inputData, inputShape);
        var output = OrtValue.Create(outputData, outputShape);

        // Warmup
        var warmupSw = Stopwatch.StartNew();
        while (warmupSw.Elapsed.TotalSeconds < warmup)
            session.Run(input, output);

        // Benchmark
        var baseTime = DateTimeOffset.UtcNow;
        var globalSw = Stopwatch.StartNew();
        var sw = new Stopwatch();
        while (globalSw.Elapsed.TotalSeconds < duration)
        {
            sw.Restart();
            session.Run(input, output);
            sw.Stop();
            var timestamp = baseTime.Add(globalSw.Elapsed);
            Console.WriteLine($"{timestamp:yyyy-MM-ddTHH:mm:ss.ffffffZ},{sw.Elapsed.TotalMilliseconds:F4}");
        }
    }

    private static void RunParallel(Model model, double warmup, int intraThreads, int interThreads,
        int parallelism, double duration, int batchSize, bool profile)
    {
        var (inputShape, outputShape) = GetShapes(model, batchSize);

        var weights = model == Model.DbNet ? EmbeddedWeights.Dbnet_Int8 : EmbeddedWeights.Svtr_Fp32;
        var sessionOptions = new SessionOptions()
            .WithIntraOpThreads(intraThreads)
            .WithInterOpThreads(interThreads)
            .WithProfiling(profile);

        // Create one session per task
        var sessions = new InferenceSession[parallelism];
        for (var i = 0; i < parallelism; i++)
            sessions[i] = new InferenceSession(weights.Bytes, sessionOptions);

        var inputSize = inputShape.Aggregate(1, (a, b) => a * b);
        var outputSize = outputShape.Aggregate(1, (a, b) => a * b);

        // Create buffers per task
        var inputs = new OrtValue[parallelism];
        var outputs = new OrtValue[parallelism];
        for (var t = 0; t < parallelism; t++)
        {
            var inputData = new float[inputSize];
            var outputData = new float[outputSize];
            var rng = new Random(42 + t);
            for (var i = 0; i < inputData.Length; i++)
                inputData[i] = rng.NextSingle();
            inputs[t] = OrtValue.Create(inputData, inputShape);
            outputs[t] = OrtValue.Create(outputData, outputShape);
        }

        // Warmup (each session)
        for (var t = 0; t < parallelism; t++)
        {
            var warmupSw = Stopwatch.StartNew();
            while (warmupSw.Elapsed.TotalSeconds < warmup)
                sessions[t].Run(inputs[t], outputs[t]);
        }

        // Benchmark - shared timing reference for all tasks
        var baseTime = DateTimeOffset.UtcNow;
        var globalSw = Stopwatch.StartNew();
        var outputLock = new object();

        var tasks = new Task[parallelism];
        for (var t = 0; t < parallelism; t++)
        {
            var taskIndex = t;
            tasks[t] = Task.Run(() =>
            {
                var session = sessions[taskIndex];
                var input = inputs[taskIndex];
                var output = outputs[taskIndex];
                var sw = new Stopwatch();

                while (globalSw.Elapsed.TotalSeconds < duration)
                {
                    sw.Restart();
                    session.Run(input, output);
                    sw.Stop();

                    lock (outputLock)
                    {
                        var timestamp = baseTime.Add(globalSw.Elapsed);
                        Console.WriteLine($"{timestamp:yyyy-MM-ddTHH:mm:ss.ffffffZ},{sw.Elapsed.TotalMilliseconds:F4}");
                    }
                }
            });
        }
        Task.WaitAll(tasks);

        // Cleanup
        foreach (var session in sessions)
            session.Dispose();
    }

    private static (int[] Input, int[] Output) GetShapes(Model model, int batchSize) => model switch
    {
        Model.DbNet => ([batchSize, 3, 640, 640], [batchSize, 640, 640]),
        Model.Svtr => ([batchSize, 3, 48, 160], [batchSize, 20, 6625]),
        _ => throw new ArgumentOutOfRangeException(nameof(model))
    };
}
