// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SpeedReader.Ocr.InferenceEngine;
using SpeedReader.Ocr.InferenceEngine.Engines;
using SpeedReader.Resources.CharDict;
using SpeedReader.Resources.Weights;

namespace SpeedReader.MicroBenchmarks.Cli;

public static class InferenceBenchmark
{
    public static void Run(Model model, double warmup, int intraThreads, int interThreads,
        int parallelism, double duration, int batchSize, bool profile)
    {
        // CpuEngine adds batch dimension internally, so input shape excludes it
        var inputShape = GetInputShape(model);

        var quantization = model == Model.DbNet ? Quantization.Int8 : Quantization.Fp32;
        var kernelOptions = new OnnxInferenceKernelOptions(model, quantization, intraThreads, interThreads, profile);
        var engineConfig = new CpuEngineConfig { Kernel = kernelOptions, Parallelism = parallelism };
        var weights = model == Model.DbNet ? EmbeddedWeights.Dbnet_Int8 : EmbeddedWeights.Svtr_Fp32;

        var services = new ServiceCollection();
        services.AddKeyedSingleton(model, engineConfig);
        services.AddKeyedSingleton(model, kernelOptions);
        services.AddKeyedSingleton(model, weights);
        services.AddKeyedSingleton<IInferenceKernel>(model, NativeOnnxInferenceKernel.Factory);
        if (model == Model.Svtr)
            services.AddSingleton(new EmbeddedCharDict());
        var serviceProvider = services.BuildServiceProvider();

        var engine = CpuEngine.Factory(serviceProvider, model);

        var inputSize = inputShape.Aggregate(1, (a, b) => a * b);

        // Create input data per task
        var inputDataArrays = new float[parallelism][];
        for (var t = 0; t < parallelism; t++)
        {
            var inputData = new float[inputSize];
            var rng = new Random(42 + t);
            for (var i = 0; i < inputData.Length; i++)
                inputData[i] = rng.NextSingle();
            inputDataArrays[t] = inputData;
        }

        var totalDuration = warmup + duration;
        var baseTime = DateTimeOffset.UtcNow;
        var globalSw = Stopwatch.StartNew();
        var outputLock = new object();

        var tasks = new Task[parallelism];
        for (var t = 0; t < parallelism; t++)
        {
            var taskIndex = t;
            tasks[t] = Task.Run(async () =>
            {
                var inputData = inputDataArrays[taskIndex];
                var sw = new Stopwatch();

                while (globalSw.Elapsed.TotalSeconds < totalDuration)
                {
                    sw.Restart();
                    await engine.Run(inputData, inputShape);
                    sw.Stop();

                    var elapsed = globalSw.Elapsed;
                    if (elapsed.TotalSeconds >= warmup && elapsed.TotalSeconds < totalDuration)
                    {
                        lock (outputLock)
                        {
                            var endTime = baseTime.Add(elapsed);
                            var startTime = endTime - sw.Elapsed;
                            Console.WriteLine($"{startTime:yyyy-MM-ddTHH:mm:ss.ffffffZ},{endTime:yyyy-MM-ddTHH:mm:ss.ffffffZ}");
                        }
                    }
                }
            });
        }
        Task.WaitAll(tasks);

        // Cleanup
        engine.DisposeAsync().AsTask().Wait();
    }

    // CpuEngine adds batch dimension, so these exclude it
    private static int[] GetInputShape(Model model) => model switch
    {
        Model.DbNet => [3, 640, 640],
        Model.Svtr => [3, 48, 160],
        _ => throw new ArgumentOutOfRangeException(nameof(model))
    };
}
