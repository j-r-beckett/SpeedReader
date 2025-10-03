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

        rootCommand.SetHandler(() =>
        {
            Console.WriteLine("Howdy");
            var cts = new CancellationTokenSource();
            var inputs = InputGenerator.GenerateImages(720, 640, 32, cts.Token);

        });

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

        microCommand.SetHandler((detection, recognition) =>
        {
            Debug.Assert(false, "Never run BenchmarkDotnet in debug mode");

            string benchmarkName;
            if (detection == recognition)
                benchmarkName = "detection and recognition benchmarks";
            else benchmarkName = detection ? "detection benchmark" : "recognition benchmark";

            var logFilePath = Path.GetTempFileName().Replace(".tmp", ".log");
            var artifactsPath = Path.GetTempPath() + Guid.NewGuid();
            var config = ManualConfig.CreateEmpty()
                .AddJob(Job.ShortRun)
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

            // Write output
            Console.WriteLine($"Mean: {mean.TotalMilliseconds:N2} ms");
            Console.WriteLine($"Err: {stdDev.TotalMilliseconds:N2} ms");
            Console.WriteLine($"Logs: {logFilePath}");
            Console.WriteLine($"Profile: {profilePath}");
        }, detectionScenarioOption, recognitionScenarioOption);

        return microCommand;
    }
}
