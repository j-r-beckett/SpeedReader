#!/usr/bin/env dotnet run
#:property EnablePreviewFeatures=true
#:project ../src/Ocr
#:project ../src/Resources
#:project ../src/Native
#:project BenchmarkUtils

using System.Diagnostics;
using BenchmarkUtils;
using SpeedReader.Ocr.InferenceEngine;

var opts = BenchmarkArgs.Parse(args);
var model = opts.GetFlag("model", Model.DbNet);
var duration = opts.GetFlag("duration", 10.0);
var warmup = opts.GetFlag("warmup", 2.0);
var interThreads = opts.GetFlag("inter-threads", 1);

// Setup inference kernel
using var ctx = InferenceKernelFactory.Create(model, interThreads: interThreads);

var totalDuration = warmup + duration;
var globalSw = Stopwatch.StartNew();

// Run inferences sequentially
while (globalSw.Elapsed.TotalSeconds < totalDuration)
{
    var token = IntegratedTimer.Start();
    ctx.Infer();
    if (globalSw.Elapsed.TotalSeconds >= warmup)
        IntegratedTimer.Stop(token);
}
