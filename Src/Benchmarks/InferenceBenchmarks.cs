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

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var pendingJobs = Channel.CreateUnbounded<Task<(float[], int[])>>();

        var jobStarter = Task.Run(async () =>
        {
            for (int i = 0; !cts.Token.IsCancellationRequested; i++)
            {
                var job = await _modelRunner.Run(modelInput[i % modelInput.Count].Data, modelInput[i % modelInput.Count].Shape);
                await pendingJobs.Writer.WriteAsync(job);
            }
            pendingJobs.Writer.Complete();
        });

        var currentSecond = 0L;
        var jobsInCurrentSecond = 0;

        var jobConsumer = Task.Run(async () =>
        {
            await foreach (var job in pendingJobs.Reader.ReadAllAsync())
            {
                var inferenceResult = await job;

                var now = Stopwatch.GetTimestamp();
                var second = now / Stopwatch.Frequency;

                if (second != currentSecond)
                {
                    if (currentSecond != 0)
                    {
                        Console.WriteLine($"{jobsInCurrentSecond:F2} tiles/sec");
                    }
                    currentSecond = second;
                    jobsInCurrentSecond = 1;
                }
                else
                {
                    jobsInCurrentSecond++;
                }
            }
        });

        await jobStarter;
        await jobConsumer;
    }
}
