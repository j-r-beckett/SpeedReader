#!/usr/bin/env dotnet run
#:property EnablePreviewFeatures=true
#:project ../src/Ocr
#:project ../src/Resources
#:project ../src/Native

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SpeedReader.Native.Threading;
using SpeedReader.Ocr.InferenceEngine;
using SpeedReader.Ocr.InferenceEngine.Engines;
using SpeedReader.Resources.CharDict;
using SpeedReader.Resources.Weights;

// Parse CLI args: -m <model> -d <duration> -w <warmup> -t <inter_threads>
var model = Model.DbNet;
var duration = 10.0;
var warmup = 2.0;
var interThreads = 1;

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
        case "-t" or "--inter-threads":
            interThreads = int.Parse(args[++i]);
            break;
    }
}

// Setup inference kernel
var inputShape = model switch
{
    Model.DbNet => new[] { 3, 640, 640 },
    Model.Svtr => new[] { 3, 48, 160 },
    _ => throw new ArgumentOutOfRangeException()
};
var batchedInputShape = new[] { 1 }.Concat(inputShape).ToArray();

var quantization = model == Model.DbNet ? Quantization.Int8 : Quantization.Fp32;
var kernelOptions = new OnnxInferenceKernelOptions(model, quantization, 1, interThreads, false);
var weights = model == Model.DbNet ? EmbeddedWeights.Dbnet_Int8 : EmbeddedWeights.Svtr_Fp32;

var services = new ServiceCollection();
services.AddKeyedSingleton(model, kernelOptions);
services.AddKeyedSingleton(model, weights);
if (model == Model.Svtr)
    services.AddSingleton(new EmbeddedCharDict());
var serviceProvider = services.BuildServiceProvider();

var kernel = NativeOnnxInferenceKernel.Factory(serviceProvider, model);

// Create input data
var inputSize = inputShape.Aggregate(1, (a, b) => a * b);
var inputData = new float[inputSize];
var rng = new Random(42);
for (var j = 0; j < inputData.Length; j++)
    inputData[j] = rng.NextSingle();

var totalDuration = warmup + duration;
var baseTime = DateTimeOffset.UtcNow;
var globalSw = Stopwatch.StartNew();

// Run inferences sequentially
while (globalSw.Elapsed.TotalSeconds < totalDuration)
{
    var sw = Stopwatch.StartNew();
    kernel.Execute(inputData, batchedInputShape);
    sw.Stop();

    var end = baseTime.Add(globalSw.Elapsed);
    var start = end - sw.Elapsed;

    var elapsed = globalSw.Elapsed;
    if (elapsed.TotalSeconds >= warmup && elapsed.TotalSeconds < totalDuration)
        Console.WriteLine($"{interThreads},{start:yyyy-MM-ddTHH:mm:ss.ffffffZ},{end:yyyy-MM-ddTHH:mm:ss.ffffffZ}");
}

// Cleanup
((IDisposable)kernel).Dispose();
serviceProvider.Dispose();
