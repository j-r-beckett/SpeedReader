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
        var rootCommand = new RootCommand();

        rootCommand.AddCommand(MicroCommand());

        rootCommand.SetHandler(() => Console.WriteLine("Howdy"));

        await rootCommand.InvokeAsync(args);

        return 0;
    }

    private static Command MicroCommand()
    {
        var microCommand = new Command("ppp", "Run pre and post processing benchmarks");

        var detectionScenarioOption = new Option<bool>("--detection", description: "Run detection benchmark");
        detectionScenarioOption.AddAlias("-d");
        microCommand.AddOption(detectionScenarioOption);

        var recognitionScenarioOption = new Option<bool>("--recognition", description: "Run recognition benchmark");
        recognitionScenarioOption.AddAlias("-r");
        microCommand.AddOption(recognitionScenarioOption);

        var tileOption = new Option<bool>("--tile", description: "Use 640x640 input size");
        microCommand.AddOption(tileOption);

        var fullOption = new Option<bool>("--full", description: "Use 1920x1080 input size");
        microCommand.AddOption(fullOption);

        microCommand.SetHandler((detection, recognition, tile, full) =>
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

            var input = InputGenerator.GenerateInput(width, height);
            var inputPath = Path.GetTempFileName().Replace(".tmp", ".png");
            input.SaveAsPng(inputPath);
            Console.WriteLine($"Input image ({width}x{height}): {inputPath}");

            Debug.Assert(false, "Never run BenchmarkDotnet in debug mode");

            string benchmarkName = detection == recognition
                ? "combined detection and recognition benchmark"
                : detection ? "detection benchmark" : "recognition benchmark";

            // Need to pass config using env vars because BenchmarkDotNet runs benchmarks in a separate process
            Environment.SetEnvironmentVariable("BENCHMARK_INPUT_WIDTH", width.ToString());
            Environment.SetEnvironmentVariable("BENCHMARK_INPUT_HEIGHT", height.ToString());

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

            Summary summary;
            if (detection == recognition)
                summary = BenchmarkRunner.Run<DetectionAndRecognitionPrePostBenchmark>(config);
            else summary = detection ? BenchmarkRunner.Run<DetectionPrePostBenchmark>(config) : BenchmarkRunner.Run<RecognitionPrePostBenchmark>(config);

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
        }, detectionScenarioOption, recognitionScenarioOption, tileOption, fullOption);

        return microCommand;
    }
}
