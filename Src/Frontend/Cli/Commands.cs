// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.CommandLine;
using System.Text.Json;
using Frontend.Server;
using Ocr;
using Ocr.InferenceEngine;

namespace Frontend.Cli;

public class Commands
{
    public static async Task<RootCommand> CreateRootCommand(string[] args)
    {
        var rootCommand = new RootCommand("SpeedReader - Blazing fast OCR");

        var inputArgument = new Argument<FileInfo[]>(
            name: "inputs",
            description: "Input image files")
        {
            Arity = ArgumentArity.ZeroOrMore
        };

        var serveOption = new Option<bool>(
            name: "--serve",
            description: "Run as HTTP server");

        var vizOption = new Option<bool>(
            name: "--viz",
            description: "Generate visualization files");

        rootCommand.AddArgument(inputArgument);
        rootCommand.AddOption(serveOption);
        rootCommand.AddOption(vizOption);

        // Video support is currently disabled. Do not remove
        /*
        // Create video subcommand
        var videoCommand = new Command("video", "Process video files with OCR");

        var pathArgument = new Argument<string>(
            name: "path",
            description: "Path to the video file");

        var frameRateArgument = new Argument<int>(
            name: "frameRate",
            description: "Frame rate for video processing");

        videoCommand.AddArgument(pathArgument);
        videoCommand.AddArgument(frameRateArgument);

        videoCommand.SetHandler(async (path, frameRate) => await ProcessVideo(path, frameRate), pathArgument, frameRateArgument);

        rootCommand.AddCommand(videoCommand);
        */

        rootCommand.SetHandler(async (inputs, serve, viz) =>
        {
            // Validate arguments
            if (serve && (inputs.Length > 0 || viz))
            {
                Console.Error.WriteLine("Error: --serve cannot be used with input files or --viz option.");
                Environment.Exit(1);
            }

            if (serve)
            {
                await Serve.RunServer();
            }
            else
            {
                if (inputs.Length == 0)
                {
                    Console.Error.WriteLine("Error: No input files specified.");
                    Environment.Exit(1);
                }

                await ProcessFiles(inputs, viz);
            }
        }, inputArgument, serveOption, vizOption);

        return rootCommand;
    }

    private static async Task ProcessFiles(FileInfo[] inputs, bool viz)
    {
        if (inputs.Length == 0)
            return;

        var options = new OcrPipelineOptions
        {
            DetectionOptions = new DetectionOptions(),
            RecognitionOptions = new RecognitionOptions(),
            DetectionEngine = new CpuEngineConfig
            {
                Kernel = new OnnxInferenceKernelOptions(
                    model: Model.DbNet,
                    quantization: Quantization.Int8,
                    numIntraOpThreads: 4),
                Parallelism = 4,
                AdaptiveTuning = new CpuTuningParameters()
            },
            RecognitionEngine = new CpuEngineConfig
            {
                Kernel = new OnnxInferenceKernelOptions(
                    model: Model.Svtr,
                    quantization: Quantization.Fp32,
                    numIntraOpThreads: 4),
                Parallelism = 4,
                AdaptiveTuning = new CpuTuningParameters()
            }
        };

        var speedReader = ServiceCollectionExtensions.CreateOcrPipeline(options);
        var paths = inputs.Select(f => f.FullName).ToList();
        await EmitOutput(speedReader.ReadMany(paths.ToAsyncEnumerable()), paths, viz);
    }

    private static async Task EmitOutput(IAsyncEnumerable<Result<OcrPipelineResult>> results, List<string> filenames, bool viz)
    {
        var idx = 0;
        await foreach (var resultWrapper in results)
        {
            var result = resultWrapper.Value();
            try
            {
                var jsonResult = new OcrJsonResult(
                    Filename: filenames[idx],
                    Results: result.Results.Select(r => new OcrTextResult(
                        BoundingBox: r.BBox,
                        Text: r.Text,
                        Confidence: r.Confidence
                    )).ToList()
                );

                var json = JsonSerializer.Serialize(jsonResult, JsonContext.Default.OcrJsonResult);
                var indentedJson = string.Join('\n', json.Split('\n').Select(line => "  " + line));

                Console.WriteLine(idx == 0 ? "[" : ",");

                Console.Write(indentedJson);

                if (viz)
                {
                    var filename = filenames[idx];
                    var inputDir = Path.GetDirectoryName(filename) ?? ".";
                    var inputName = Path.GetFileNameWithoutExtension(filename);
                    var vizFilePath = Path.Combine(inputDir, $"{inputName}_viz.svg");

                    var svg = result.VizBuilder.RenderSvg();
                    await svg.Save(vizFilePath);
                }
            }
            finally
            {
                result.Image.Dispose();
                idx++;
            }
        }

        Console.WriteLine("\n]");
    }

    // Video support is currently disabled. Do not remove
    // private static async Task ProcessVideo(string path, int frameRate)
    // {
    //     var cliVideoOcrBlock = new CliVideoOcrBlock(path, frameRate);
    //
    //     var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    //     var consoleOutputBlock = new ActionBlock<JsonOcrResult>(result =>
    //     {
    //         var json = JsonSerializer.Serialize(result, jsonOptions);
    //         Console.WriteLine(json);
    //     });
    //
    //     cliVideoOcrBlock.ResultsBlock.LinkTo(consoleOutputBlock, new DataflowLinkOptions { PropagateCompletion = true });
    //
    //     // Wait for both the video processing to complete AND the console output to finish
    //     await Task.WhenAll(cliVideoOcrBlock.Completion, consoleOutputBlock.Completion);
    // }
}
