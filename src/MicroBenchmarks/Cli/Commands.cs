// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.CommandLine;
using BenchmarkDotNet.Running;
using SpeedReader.Ocr.InferenceEngine;

namespace SpeedReader.MicroBenchmarks.Cli;

public static class Commands
{
    public static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("MicroBenchmarks - Performance benchmarks for SpeedReader");

        rootCommand.AddCommand(CreateBdnCommand());
        rootCommand.AddCommand(CreateInferenceCommand());

        return rootCommand;
    }

    private static Command CreateBdnCommand()
    {
        var command = new Command("bdn", "Run BenchmarkDotNet benchmarks");

        command.SetHandler(() =>
        {
            BenchmarkRunner.Run<DryPipelineBenchmark>();
            BenchmarkRunner.Run<StartupBenchmark>();
        });

        return command;
    }

    private static Command CreateInferenceCommand()
    {
        var command = new Command("inference", "Run inference benchmarks");

        var modelOption = new Option<string>(
            aliases: ["-m", "--model"],
            description: "Model to benchmark (dbnet or svtr)")
        {
            IsRequired = true
        };

        var warmupOption = new Option<int>(
            aliases: ["-w", "--warmup"],
            getDefaultValue: () => 10,
            description: "Number of warmup iterations");

        var intraThreadsOption = new Option<int>(
            name: "--intra-threads",
            getDefaultValue: () => 1,
            description: "Intra-op threads (parallelism within a single operator)");

        var interThreadsOption = new Option<int>(
            name: "--inter-threads",
            getDefaultValue: () => 1,
            description: "Inter-op threads (parallelism between operators)");

        var parallelismOption = new Option<int>(
            aliases: ["-p", "--parallelism"],
            getDefaultValue: () => 1,
            description: "Application parallelism (concurrent tasks calling inference)");

        var iterationsOption = new Option<int>(
            aliases: ["-n", "--iterations"],
            getDefaultValue: () => 100,
            description: "Number of benchmark iterations");

        var batchSizeOption = new Option<int>(
            aliases: ["-b", "--batch-size"],
            getDefaultValue: () => 1,
            description: "Batch size for inference");

        var profileOption = new Option<bool>(
            name: "--profile",
            description: "Enable ONNX profiling (writes to current directory)");

        command.AddOption(modelOption);
        command.AddOption(warmupOption);
        command.AddOption(intraThreadsOption);
        command.AddOption(interThreadsOption);
        command.AddOption(parallelismOption);
        command.AddOption(iterationsOption);
        command.AddOption(batchSizeOption);
        command.AddOption(profileOption);

        command.SetHandler((modelName, warmup, intraThreads, interThreads, parallelism, iterations, batchSize, profile) =>
        {
            var model = modelName.ToLowerInvariant() switch
            {
                "dbnet" => Model.DbNet,
                "svtr" => Model.Svtr,
                _ => throw new ArgumentException($"Unknown model: {modelName}. Use 'dbnet' or 'svtr'.")
            };

            InferenceBenchmark.Run(model, warmup, intraThreads, interThreads, parallelism, iterations, batchSize, profile);
        }, modelOption, warmupOption, intraThreadsOption, interThreadsOption, parallelismOption, iterationsOption, batchSizeOption, profileOption);

        return command;
    }
}
