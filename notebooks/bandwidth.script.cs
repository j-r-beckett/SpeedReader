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

// Parse CLI args: -m <model> -d <duration> -w <warmup> -c <cores...> [--intra-threads N] [--inter-threads N] [--profile]
var model = Model.DbNet;
var duration = 10.0;
var warmup = 2.0;
var cores = new List<int>();
var intraThreads = 1;
var interThreads = 1;
var profile = false;

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
        case "-c" or "--cores":
            while (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                cores.Add(int.Parse(args[++i]));
            break;
        case "--intra-threads":
            intraThreads = int.Parse(args[++i]);
            break;
        case "--inter-threads":
            interThreads = int.Parse(args[++i]);
            break;
        case "--profile":
            profile = true;
            break;
    }
}

if (cores.Count == 0)
    cores.Add(0);

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
    while (pending.Count < cores.Count)
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
