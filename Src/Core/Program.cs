using System.CommandLine;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using Ocr;
using Ocr.Blocks;
using Ocr.Visualization;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Core;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Create root command
        var rootCommand = new RootCommand("SpeedReader - Blazing fast OCR");

        // Add arguments and options
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
        // Initialize metrics
        var meter = new Meter("SpeedReader.Ocr");

        // Create CLI OCR pipeline
        var config = new CliOcrBlock.Config
        {
            VizMode = vizMode,
            JsonOutput = jsonOutput,
            Meter = meter
        };
        var cliOcrBlock = new CliOcrBlock(config);

        // Send all filenames to the pipeline
        foreach (var input in inputs)
        {
            await cliOcrBlock.Target.SendAsync(input.FullName);
        }

        // Complete the pipeline and await completion
        cliOcrBlock.Target.Complete();
        await cliOcrBlock.Completion;
    }


}
