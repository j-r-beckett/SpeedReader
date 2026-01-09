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
var duration = opts.GetFlag("duration", 10.0);
var warmup = opts.GetFlag("warmup", 2.0);
var svtrCores = opts.GetFlag("svtr-cores", Array.Empty<int>());
var dbnetCores = opts.GetFlag("dbnet-cores", Array.Empty<int>());

var totalDuration = warmup + duration;
var globalSw = Stopwatch.StartNew();

// Setup inference kernels and thread pools
using var svtrCtx = svtrCores.Length > 0 ? InferenceKernelFactory.Create(Model.Svtr) : null;
using var dbnetCtx = dbnetCores.Length > 0 ? InferenceKernelFactory.Create(Model.DbNet) : null;

var svtrPool = svtrCores.Length > 0 ? new AffinitizedThreadPool(svtrCores) : null;
var dbnetPool = dbnetCores.Length > 0 ? new AffinitizedThreadPool(dbnetCores) : null;

// Submit work continuously; thread pools handle parallelism
var svtrPending = new List<Task>();
var dbnetPending = new List<Task>();

while (globalSw.Elapsed.TotalSeconds < totalDuration)
{
    // Keep SVTRv2 pool saturated
    if (svtrPool != null)
    {
        while (svtrPending.Count < svtrCores.Length * 1.5)
        {
            svtrPending.Add(svtrPool.Run(() =>
            {
                var coreId = Affinitizer.GetCurrentCpu();
                var token = IntegratedTimer.Start();
                token.Tags["model"] = "svtr";
                token.Tags["core_id"] = coreId.ToString();
                svtrCtx!.Infer();
                if (globalSw.Elapsed.TotalSeconds >= warmup)
                    IntegratedTimer.Stop(token);
            }));
        }
    }

    // Keep DbNet pool saturated
    if (dbnetPool != null)
    {
        while (dbnetPending.Count < dbnetCores.Length * 1.5)
        {
            dbnetPending.Add(dbnetPool.Run(() =>
            {
                var coreId = Affinitizer.GetCurrentCpu();
                var token = IntegratedTimer.Start();
                token.Tags["model"] = "dbnet";
                token.Tags["core_id"] = coreId.ToString();
                dbnetCtx!.Infer();
                if (globalSw.Elapsed.TotalSeconds >= warmup)
                    IntegratedTimer.Stop(token);
            }));
        }
    }

    // Wait for any to complete
    var allPending = svtrPending.Concat(dbnetPending).ToList();
    if (allPending.Count > 0)
    {
        var completed = await Task.WhenAny(allPending);
        svtrPending.Remove(completed);
        dbnetPending.Remove(completed);
        await completed;
    }
}

// Cleanup
svtrPool?.Dispose();
dbnetPool?.Dispose();
