// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.CommandLine;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using Experimental;
using Experimental.Inference;
using Ocr;
using Ocr.Visualization;
using Resources;

namespace Core;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("SPEEDREADER_DEBUG_WAIT")?.ToLower() == "true")
        {
            Console.WriteLine("Waiting for debugger to attach");
            while (!Debugger.IsAttached)
            {
                await Task.Delay(25);
            }
            Console.WriteLine("Debugger attached");
        }

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

        var vizOption = new Option<VizMode>(
            name: "--viz",
            description: "Visualization mode",
            getDefaultValue: () => VizMode.None);

        var jsonOption = new Option<bool>(
            name: "--json",
            description: "Full JSON output with detailed metadata and confidence scores");

        rootCommand.AddArgument(inputArgument);
        rootCommand.AddOption(serveOption);
        rootCommand.AddOption(vizOption);
        rootCommand.AddOption(jsonOption);

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

        rootCommand.SetHandler(async (inputs, serve, vizMode, jsonOutput) =>
        {
            // Validate arguments
            if (serve && (inputs.Length > 0 || vizMode != VizMode.None || jsonOutput))
            {
                Console.Error.WriteLine("Error: --serve cannot be used with input files, --viz option, or --json option.");
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

                await ProcessFiles(inputs, vizMode, jsonOutput);
            }
        }, inputArgument, serveOption, vizOption, jsonOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task ProcessFiles(FileInfo[] inputs, VizMode vizMode, bool jsonOutput)
    {
        if (inputs.Length == 0)
            return;

        var modelProvider = new ModelProvider();
        var dbnetRunner = new CpuModelRunner(modelProvider.GetSession(Model.DbNet18, ModelPrecision.INT8), 4);
        var svtrRunner = new CpuModelRunner(modelProvider.GetSession(Model.SVTRv2), 4);
        var speedReader = new SpeedReader(dbnetRunner, svtrRunner, 4, 1);
        var paths = inputs.Select(f => f.FullName).ToList();
        await EmitOutput(speedReader.ReadMany(paths.ToAsyncEnumerable()), paths, vizMode);
    }

    private static async Task EmitOutput(IAsyncEnumerable<SpeedReaderResult> results, List<string> filenames, VizMode vizMode)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var idx = 0;
        await foreach (var result in results)
        {
            try
            {
                var jsonResult = new
                {
                    Filename = filenames[idx],
                    Results = result.Results.Select(r => new
                    {
                        BoundingBox = r.BBox,
                        r.Text,
                        r.Confidence
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(jsonResult, jsonOptions);
                var indentedJson = string.Join('\n', json.Split('\n').Select(line => "  " + line));

                Console.WriteLine(idx == 0 ? "[" : ",");

                Console.Write(indentedJson);

                // Generate visualization if configured
                if (vizMode != VizMode.None)
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
