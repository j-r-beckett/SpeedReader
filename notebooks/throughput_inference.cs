#!/usr/bin/env dotnet run
#:property EnablePreviewFeatures=true
#:project ../src/Ocr
#:project ../src/Resources

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SpeedReader.Ocr.InferenceEngine;
using SpeedReader.Ocr.InferenceEngine.Engines;
using SpeedReader.Resources.CharDict;
using SpeedReader.Resources.Weights;

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

// Setup inference engine
var inputShape = model switch
{
    Model.DbNet => new[] { 3, 640, 640 },
    Model.Svtr => new[] { 3, 48, 160 },
    _ => throw new ArgumentOutOfRangeException()
};

var quantization = model == Model.DbNet ? Quantization.Int8 : Quantization.Fp32;
var kernelOptions = new OnnxInferenceKernelOptions(model, quantization, intraThreads, interThreads, profile);
var engineConfig = new CpuEngineConfig { Kernel = kernelOptions, Cores = [.. cores] };
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
var inputDataArrays = new float[cores.Count][];
for (var t = 0; t < cores.Count; t++)
{
    var inputData = new float[inputSize];
    var rng = new Random(42 + t);
    for (var j = 0; j < inputData.Length; j++)
        inputData[j] = rng.NextSingle();
    inputDataArrays[t] = inputData;
}

var totalDuration = warmup + duration;
var baseTime = DateTimeOffset.UtcNow;
var globalSw = Stopwatch.StartNew();
var outputLock = new object();

var tasks = new Task[cores.Count];
for (var t = 0; t < cores.Count; t++)
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
serviceProvider.Dispose();
