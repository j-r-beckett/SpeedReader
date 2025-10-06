// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.CommandLine;
using System.Diagnostics;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Analysers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Benchmarks;
using Resources;
using SixLabors.ImageSharp;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Debug.Assert(false, "Always run benchmarks in Release mode");

        var rootCommand = new RootCommand();

        rootCommand.AddCommand(PppCommand());

        rootCommand.AddCommand(InferenceCommand());

        rootCommand.AddCommand(SystemCommand());

        await rootCommand.InvokeAsync(args);

        return 0;
    }

    private static Command InferenceCommand()
    {
        var inferenceCommand = new Command("inference", "Run inference benchmarks");

        var dbnetScenarioOption = new Option<bool>("--dbnet", description: "Run dbnet benchmark");
        dbnetScenarioOption.AddAlias("-d");
        inferenceCommand.AddOption(dbnetScenarioOption);

        var svtrScenarioOption = new Option<bool>("--svtr", description: "Run SVTRv2 benchmark");
        svtrScenarioOption.AddAlias("-s");
        inferenceCommand.AddOption(svtrScenarioOption);

        var threadsOption = new Option<string>("--threads", description: "Number of threads to use", getDefaultValue: () => "1");
        inferenceCommand.AddOption(threadsOption);

        var intraOpThreadsOption = new Option<string>("--intra-op-threads", description: "Number of intra-op threads to use", getDefaultValue: () => "1");
        inferenceCommand.AddOption(intraOpThreadsOption);

        var quantizationOption = new Option<string>("--quantization", description: "Quantization mode (fp32 or int8)", getDefaultValue: () => "fp32");
        quantizationOption.AddAlias("-q");
        inferenceCommand.AddOption(quantizationOption);

        var testPeriodOption = new Option<int>("--test-period", description: "Test period in seconds", getDefaultValue: () => 10);
        inferenceCommand.AddOption(testPeriodOption);

        var totalThreadsOption = new Option<int?>("--total-threads", description: "Total threads (runs all combinations where threads * intra-op-threads = total-threads)", getDefaultValue: () => null);
        inferenceCommand.AddOption(totalThreadsOption);

        inferenceCommand.SetHandler(async (dbnet, svtr, threads, intraOpThreads, quantizationStr, testPeriod, totalThreads) =>
        {
            if (dbnet == svtr) throw new ArgumentException("Specify exactly one of --dbnet or --svtr");

            if (totalThreads.HasValue)
            {
                if (threads != "1" || intraOpThreads != "1")
                    throw new ArgumentException("Cannot specify --total-threads with --threads or --intra-op-threads");
            }
            else
            {
                if (threads != "1" && intraOpThreads != "1")
                    throw new ArgumentException("Cannot specify both --threads and --intra-op-threads");
            }

            var quantization = quantizationStr.ToLower() switch
            {
                "fp32" => ModelPrecision.FP32,
                "int8" => ModelPrecision.INT8,
                _ => throw new InvalidOperationException($"Invalid quantization value: {quantizationStr}. Must be 'fp32' or 'int8'.")
            };

            IEnumerable<(int Threads, int IntraOpThreads)> GetThreads()
            {
                if (totalThreads.HasValue)
                {
                    for (int t = totalThreads.Value; t >= 1; t--)
                    {
                        if (totalThreads.Value % t == 0)
                        {
                            int i = totalThreads.Value / t;
                            yield return (t, i);
                        }
                    }
                }
                else
                {
                    (int start, int end) ParseSpec(string spec)
                    {
                        if (int.TryParse(spec, out var value))
                        {
                            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
                            return (value, value);
                        }

                        var match = Regex.Match(spec, @"^(\d+)\.\.(\d+)$");
                        if (!match.Success)
                            throw new ArgumentException($"Invalid value: '{spec}'. Expected a number or a range like '2..4'.");

                        var start = int.Parse(match.Groups[1].Value);
                        var end = int.Parse(match.Groups[2].Value);

                        ArgumentOutOfRangeException.ThrowIfLessThan(start, 1);
                        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);

                        return (start, end);
                    }

                    var (threadStart, threadEnd) = ParseSpec(threads);
                    var (intraStart, intraEnd) = ParseSpec(intraOpThreads);

                    for (int t = threadStart; t <= threadEnd; t++)
                    {
                        for (int i = intraStart; i <= intraEnd; i++)
                        {
                            yield return (t, i);
                        }
                    }
                }
            }

            var model = dbnet ? Model.DbNet18 : Model.SVTRv2;
            var modelName = dbnet ? "DBNet" : "SVTRv2";
            var modelInputs = dbnet
                ? DBNetBenchmarkHelper.GenerateInput(640, 640, Density.Low)
                : SVTRv2BenchmarkHelper.GenerateInput();

            // Print benchmark configuration
            Console.WriteLine($"{modelName} Inference Benchmark");
            Console.WriteLine($"Quantization: {quantization}");
            Console.WriteLine($"Test period: {testPeriod}s");
            if (totalThreads.HasValue)
            {
                Console.WriteLine($"Total threads: {totalThreads.Value}");
            }
            else
            {
                Console.WriteLine($"Threads: {threads}");
                Console.WriteLine($"Intra-op threads: {intraOpThreads}");
            }
            Console.WriteLine();

            var results = new List<(int threads, int intraOpThreads, double throughput, double bandwidth)>();

            foreach (var (t, i) in GetThreads())
            {
                var benchmark = new InferenceBenchmark(model, t, i, quantization, testPeriod);
                var counter = new SimpleTimeCounter($"(threads={t}, intra op threads={i})");
                counter.OnStartBuildStage([]);
                var (completed, time, bandwidth) = await benchmark.RunBenchmark(modelInputs);
                counter.OnEndRunStage();

                var throughput = completed / time.TotalSeconds;
                results.Add((t, i, throughput, bandwidth));
                benchmark.Cleanup();
            }

            // Print summary table
            Console.WriteLine();
            Console.WriteLine("Summary:");
            Console.WriteLine($"{"Threads",-10} {"Intra-op",-10} {"Throughput (inferences/sec)",20} {"Memory BW (GB/s)",20}");
            Console.WriteLine(new string('-', 62));
            foreach (var (t, i, throughput, bandwidth) in results)
            {
                Console.WriteLine($"{t,-10} {i,-10} {throughput,20:N2} {bandwidth,20:N2}");
            }
        }, dbnetScenarioOption, svtrScenarioOption, threadsOption, intraOpThreadsOption, quantizationOption, testPeriodOption, totalThreadsOption);

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

    private static Command SystemCommand()
    {
        var systemCommand = new Command("system", "Run system benchmarks");

        var bwOption = new Option<bool>("--bw", description: "Run memory bandwidth benchmark");
        systemCommand.AddOption(bwOption);

        var coresOption = new Option<int>("--cores", description: "Number of cores to use", getDefaultValue: () => Environment.ProcessorCount);
        systemCommand.AddOption(coresOption);

        var testPeriodOption = new Option<int>("--test-period", description: "Test period in seconds", getDefaultValue: () => 1);
        systemCommand.AddOption(testPeriodOption);

        systemCommand.SetHandler((bw, cores, testPeriod) =>
        {
            if (!bw) throw new ArgumentException("The only supported benchmark is --bw");

            Console.WriteLine("System Memory Bandwidth Benchmark");
            Console.WriteLine($"Cores: {cores}");
            Console.WriteLine($"Test period: {testPeriod}s");
            Console.WriteLine();

            var benchmark = new SystemMemoryBandwidthBenchmark(cores, testPeriod);
            var bandwidth = benchmark.RunBenchmark();

            Console.WriteLine($"Memory Bandwidth: {bandwidth:N2} GB/s");
        }, bwOption, coresOption, testPeriodOption);

        return systemCommand;
    }
}
