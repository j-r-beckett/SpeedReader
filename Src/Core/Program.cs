using System.CommandLine;
using System.Diagnostics.Metrics;
using System.Text.Json;
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

        rootCommand.AddArgument(inputArgument);
        rootCommand.AddOption(serveOption);
        rootCommand.AddOption(vizOption);

        rootCommand.SetHandler(async (inputs, serve, vizMode) =>
        {
            // Validate arguments
            if (serve && (inputs.Length > 0 || vizMode != VizMode.None))
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

                await ProcessFiles(inputs, vizMode);
            }
        }, inputArgument, serveOption, vizOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task ProcessFiles(FileInfo[] inputs, VizMode vizMode)
    {
        foreach (var input in inputs)
        {
            // Generate output filename with _viz suffix
            var inputDir = Path.GetDirectoryName(input.FullName) ?? ".";
            var inputName = Path.GetFileNameWithoutExtension(input.FullName);
            var inputExt = Path.GetExtension(input.FullName);
            var outputPath = Path.Combine(inputDir, $"{inputName}_viz{inputExt}");
            var output = new FileInfo(outputPath);

            await ProcessFile(input, output, vizMode);
        }
    }

    private static async Task ProcessFile(FileInfo input, FileInfo output, VizMode vizMode)
    {
        try
        {
            // Validate input file exists
            if (!input.Exists)
            {
                Console.Error.WriteLine($"Error: Input file '{input.FullName}' not found.");
                Environment.Exit(1);
            }

            // Load input image
            using var image = await Image.LoadAsync<Rgb24>(input.FullName);

            // Initialize metrics
            var meter = new Meter("SpeedReader.Ocr");


            // Create OCR pipeline
            using var modelProvider = new ModelProvider();
            var dbnetSession = modelProvider.GetSession(Model.DbNet18, ModelPrecision.INT8);
            var svtrSession = modelProvider.GetSession(Model.SVTRv2);

            var ocrBlock = OcrBlock.Create(dbnetSession, svtrSession, new OcrConfiguration(), meter);
            await using var ocrBridge = new DataflowBridge<(Image<Rgb24>, VizBuilder), (Image<Rgb24>, OcrResult, VizBuilder)>(ocrBlock);

            // Create VizBuilder and process through bridge
            var vizBuilder = VizBuilder.Create(vizMode, image);
            var resultTask = await ocrBridge.ProcessAsync((image, vizBuilder), CancellationToken.None, CancellationToken.None);
            var result = await resultTask;

            // Update page number
            var ocrResults = result.Item2 with { PageNumber = 0 };

            // Output JSON to stdout
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(ocrResults, jsonOptions);
            Console.WriteLine(json);

            // Generate visualization using VizBuilder
            if (vizMode != VizMode.None)
            {
                var outputImage = result.Item3.Render();

                // Save output image
                await outputImage.SaveAsync(output.FullName);
                Console.WriteLine($"OCR visualization saved to: {output.FullName}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine($"Stack trace: {ex}");
            Environment.Exit(1);
        }
    }

}
