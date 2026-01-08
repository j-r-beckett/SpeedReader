#!/usr/bin/env dotnet run
#:property EnablePreviewFeatures=true
#:project ../src/Ocr
#:project ../src/Resources
#:project ../src/Native
#:project BenchmarkUtils

using System.Diagnostics;
using BenchmarkUtils;
using SpeedReader.Ocr.InferenceEngine;


// Parse CLI args: -m <model> -d <duration> -w <warmup> -b <batch_size>
var model = Model.DbNet;
var duration = 10.0;
var warmup = 2.0;
var batchSize = 1;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-m" or "--model":
            model = args[++i].ToLowerInvariant() switch
            {
                "dbnet" => Model.DbNet,
                "svtr" => Model.Svtr,
                _ => throw new ArgumentException($"Unknown model: {args[i]}")
            };
            break;
        case "-d" or "--duration":
            duration = double.Parse(args[++i]);
            break;
        case "-w" or "--warmup":
            warmup = double.Parse(args[++i]);
            break;
        case "-b" or "--batch-size":
            batchSize = int.Parse(args[++i]);
            break;
    }
}

// Setup inference kernel
using var ctx = InferenceKernelFactory.Create(model);

var totalDuration = warmup + duration;
var globalSw = Stopwatch.StartNew();

// Run inferences sequentially on a single core
while (globalSw.Elapsed.TotalSeconds < totalDuration)
{
    var token = IntegratedTimer.Start();
    ctx.Infer(batchSize);
    if (globalSw.Elapsed.TotalSeconds >= warmup)
        IntegratedTimer.Stop(token);
}
