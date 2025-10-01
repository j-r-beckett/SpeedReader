// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.CommandLine;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Running;
using Benchmarks;
using Benchmarks.MicroBenchmarks;
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

        rootCommand.AddCommand(PppCommand());

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
        var microCommand = new Command("micro", "Run micro benchmarks");

        microCommand.SetHandler(() =>
        {
            BenchmarkRunner.Run<Detection>();
        });

        return microCommand;
    }

    private static Command PppCommand()
    {
        var pppCommand = new Command("ppp", "Run pre and post processing benchmark");

        var threadsOption = new Option<int>("--threads", description: "Number of threads to use");
        threadsOption.SetDefaultValue(1);
        var inputWidthOption = new Option<int>("--input-width", description: "Input width");
        inputWidthOption.SetDefaultValue(640);
        var inputHeightOption = new Option<int>("--input-height", description: "Input height");
        inputHeightOption.SetDefaultValue(640);
        var densityOption = new Option<int>("--density", description: "Number of text items to generate in the input");
        densityOption.SetDefaultValue(32);
        var durationOption = new Option<int>("--duration", description: "Duration of the benchmark in seconds");
        durationOption.SetDefaultValue(10);

        pppCommand.AddOption(threadsOption);
        pppCommand.AddOption(inputWidthOption);
        pppCommand.AddOption(inputHeightOption);
        pppCommand.AddOption(densityOption);
        pppCommand.AddOption(durationOption);

        pppCommand.SetHandler(async (threads, inputWidth, inputHeight, density, durationSeconds) =>
        {
            var image = await InputGenerator.GenerateImages(inputWidth, inputHeight, density, CancellationToken.None).FirstAsync();

            var dbnetSession = new ModelProvider().GetSession(Model.DbNet18, ModelPrecision.INT8);
            var dbnetRunner = new CachingModelRunner(dbnetSession);
            var svtrSession = new ModelProvider().GetSession(Model.SVTRv2, ModelPrecision.FP32);
            var svtrRunner = new CachingModelRunner(svtrSession);
            var reader = new SpeedReader(dbnetRunner, svtrRunner, threads);

            await await reader.ReadOne(image);  // Warm up

            var timer = Stopwatch.StartNew();
            var counter = 0;

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
            await foreach (var _ in reader.ReadMany(Repeat(image, cts.Token)))
            {
                counter++;
            }

            var throughput = (double)counter / timer.ElapsedMilliseconds * 1000;
            Console.WriteLine($"Processed {counter} frames in {timer.ElapsedMilliseconds} ms. Throughput: {throughput:N2} items/sec");

        }, threadsOption, inputWidthOption, inputHeightOption, densityOption, durationOption);

        return pppCommand;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        async IAsyncEnumerable<Image<Rgb24>> Repeat(Image<Rgb24> image, [EnumeratorCancellation] CancellationToken cancellationToken)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                yield return image.Clone();
            }
        }
    }
}
