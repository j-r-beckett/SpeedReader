// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.CommandLine;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Analysers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Benchmarks;
using Core;
using Experimental;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Debug.Assert(false, "Always run benchmarks in Release mode");

        var rootCommand = new RootCommand();

        rootCommand.AddCommand(PppCommand());

        rootCommand.AddCommand(InferenceCommand());

        await rootCommand.InvokeAsync(args);

        return 0;
    }

    private static Command InferenceCommand()
    {
        var inferenceCommand = new Command("inference", "Run inference benchmarks");

        var dbnetScenarioOption = new Option<bool>("--dbnet", description: "Run dbnet benchmark");
        dbnetScenarioOption.AddAlias("-d");
        inferenceCommand.AddOption(dbnetScenarioOption);

        var tileOption = new Option<bool>("--tile", description: "Use 640x640 input size");
        inferenceCommand.AddOption(tileOption);

        var fullOption = new Option<bool>("--full", description: "Use 1920x1080 input size");
        inferenceCommand.AddOption(fullOption);

        var densityOption = new Option<string>("--density", description: "Text density (low or high)", getDefaultValue: () => "high");
        inferenceCommand.AddOption(densityOption);

        var threadsOption = new Option<int>("--threads", description: "Number of threads to use", getDefaultValue: () => 1);
        inferenceCommand.AddOption(threadsOption);

        var quantizationOption = new Option<string>("--quantization", description: "Quantization mode (fp32 or int8)", getDefaultValue: () => "fp32");
        quantizationOption.AddAlias("-q");
        inferenceCommand.AddOption(quantizationOption);

        inferenceCommand.SetHandler(async (dbnet, tile, full, densityStr, threads, quantizationStr) =>
        {
            if (!dbnet) throw new ArgumentException("The only supported scenario is dbnet");

            if (tile && full)
                throw new InvalidOperationException("Cannot specify both --tile and --full");

            var density = densityStr.ToLower() switch
            {
                "low" => Density.Low,
                "high" => Density.High,
                _ => throw new InvalidOperationException($"Invalid density value: {densityStr}. Must be 'low' or 'high'.")
            };

            var quantization = quantizationStr.ToLower() switch
            {
                "fp32" => ModelPrecision.FP32,
                "int8" => ModelPrecision.INT8,
                _ => throw new InvalidOperationException($"Invalid quantization value: {quantizationStr}. Must be 'fp32' or 'int8'.")
            };

            var input = InputGenerator.GenerateInput(tile ? 1920 : 640, tile ? 1080 : 640, density);

            var benchmark = new DBNetBenchmark(threads, 1, quantization);

            await benchmark.RunBenchmark(input);
        }, dbnetScenarioOption, tileOption, fullOption, densityOption, threadsOption, quantizationOption);

        return inferenceCommand;
    }

    private static Command PppCommand()
    {
        var pppCommand = new Command("ppp", "Run pre and post processing benchmarks");

        var detectionScenarioOption = new Option<bool>("--detection", description: "Run detection benchmark");
        detectionScenarioOption.AddAlias("-d");
        pppCommand.AddOption(detectionScenarioOption);

        var recognitionScenarioOption = new Option<bool>("--recognition", description: "Run recognition benchmark");
        recognitionScenarioOption.AddAlias("-r");
        pppCommand.AddOption(recognitionScenarioOption);

        var tileOption = new Option<bool>("--tile", description: "Use 640x640 input size");
        pppCommand.AddOption(tileOption);

        var fullOption = new Option<bool>("--full", description: "Use 1920x1080 input size");
        pppCommand.AddOption(fullOption);

        var densityOption = new Option<string>("--density", description: "Text density (low or high)", getDefaultValue: () => "high");
        pppCommand.AddOption(densityOption);

        pppCommand.SetHandler((detection, recognition, tile, full, densityStr) =>
        {
            if (tile && full)
                throw new InvalidOperationException("Cannot specify both --tile and --full");

            int width, height;
            if (full)
            {
                width = 1920;
                height = 1080;
            }
            else
            {
                width = 640;
                height = 640;
            }

            var density = densityStr.ToLower() switch
            {
                "low" => Density.Low,
                "high" => Density.High,
                _ => throw new InvalidOperationException($"Invalid density value: {densityStr}. Must be 'low' or 'high'.")
            };

            var input = InputGenerator.GenerateInput(width, height, density);
            var inputPath = Path.GetTempFileName().Replace(".tmp", ".png");
            input.SaveAsPng(inputPath);
            Console.WriteLine($"Input image ({width}x{height}): {inputPath}");

            string benchmarkName = detection == recognition
                ? "combined detection and recognition benchmark"
                : detection ? "detection benchmark" : "recognition benchmark";

            // Need to pass config using env vars because BenchmarkDotNet runs benchmarks in a separate process
            Environment.SetEnvironmentVariable("BENCHMARK_INPUT_WIDTH", width.ToString());
            Environment.SetEnvironmentVariable("BENCHMARK_INPUT_HEIGHT", height.ToString());
            Environment.SetEnvironmentVariable("BENCHMARK_DENSITY", densityStr.ToLower());

            var logFilePath = Path.GetTempFileName().Replace(".tmp", ".log");
            var artifactsPath = Path.GetTempPath() + Guid.NewGuid();
            var config = ManualConfig.CreateEmpty()
                // .AddJob(Job.ShortRun)
                .AddJob(Job.Default)
                .WithArtifactsPath(artifactsPath)
                .AddColumnProvider(DefaultColumnProviders.Instance)
                .AddDiagnoser(EventPipeProfiler.Default)
                .AddAnalyser(EnvironmentAnalyser.Default)
                .AddLogger(new StreamLogger(logFilePath))
                .AddEventProcessor(new SimpleTimeCounter(benchmarkName));

            Summary summary = detection == recognition
                ? BenchmarkRunner.Run<DetectionAndRecognitionPrePostBenchmark>(config)
                : detection ? BenchmarkRunner.Run<DetectionPrePostBenchmark>(config) : BenchmarkRunner.Run<RecognitionPrePostBenchmark>(config);

            Console.WriteLine($"Logs: {logFilePath}");

            if (summary.HasCriticalValidationErrors)
                throw new InvalidOperationException($"Benchmark failed with critical validation errors: {string.Join(", ", summary.ValidationErrors)}");

            if (summary.Reports.Length != 1)
                throw new InvalidOperationException($"Expected exactly 1 benchmark report, but got {summary.Reports.Length}");

            // BenchmarkDotNet returns times in nanoseconds. nanoseconds -> ticks -> TimeSpan
            var stats = summary.Reports[0].ResultStatistics!;
            var mean = TimeSpan.FromTicks((long)(stats.Mean / 100));
            var stdDev = TimeSpan.FromTicks((long)(stats.StandardDeviation / 100));

            // Find where BenchmarkDotNet saved the profile data
            var rawProfilePaths = Directory.GetFiles(artifactsPath, "*.speedscope.json", SearchOption.AllDirectories);
            Array.Sort(rawProfilePaths);
            var rawProfilePath = rawProfilePaths.First();

            var profilePath = Path.GetTempFileName().Replace(".tmp", ".speedscope.json");
            File.Copy(rawProfilePath, profilePath, overwrite: true);

            Console.WriteLine($"Mean: {mean.TotalMilliseconds:N2} ms");
            Console.WriteLine($"Err: {stdDev.TotalMilliseconds:N2} ms");
            Console.WriteLine($"Profile: {profilePath}");
        }, detectionScenarioOption, recognitionScenarioOption, tileOption, fullOption, densityOption);

        return pppCommand;
    }
}
