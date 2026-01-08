#!/usr/bin/env dotnet run
#:property EnablePreviewFeatures=true
#:project ../src/Ocr
#:project ../src/Resources
#:project ../src/Native
#:project BenchmarkUtils

using System.Diagnostics;
using BenchmarkUtils;
using SpeedReader.Native.Threading;
using SpeedReader.Ocr.InferenceEngine;
using SpeedReader.Ocr.InferenceEngine.Engines;

var opts = BenchmarkArgs.Parse(args);
var model = opts.GetFlag("model", Model.DbNet);
var duration = opts.GetFlag("duration", 10.0);
var warmup = opts.GetFlag("warmup", 2.0);
var cores = opts.GetFlag("cores", new[] { 0 });
var intraThreads = opts.GetFlag("intra-threads", 1);
var interThreads = opts.GetFlag("inter-threads", 1);
var profile = opts.GetFlag("profile", false);

// Setup inference kernel and thread pool
using var ctx = InferenceKernelFactory.Create(model, intraThreads, interThreads, profile);
var threadPool = new AffinitizedThreadPool(cores);

var totalDuration = warmup + duration;
var globalSw = Stopwatch.StartNew();

// Submit work continuously; threadPool handles parallelism
var pending = new List<Task>();
while (globalSw.Elapsed.TotalSeconds < totalDuration)
{
    // Keep the pool saturated
    while (pending.Count < cores.Length)
    {
        pending.Add(threadPool.Run(() =>
        {
            var coreId = Affinitizer.GetCurrentCpu();
            var token = IntegratedTimer.Start();
            token.Tags["core_id"] = coreId.ToString();
            ctx.Infer();
            if (globalSw.Elapsed.TotalSeconds >= warmup)
                IntegratedTimer.Stop(token);
        }));
    }

    // Wait for any to complete
    var completed = await Task.WhenAny(pending);
    pending.Remove(completed);
    await completed;
}

// Cleanup
threadPool.Dispose();
