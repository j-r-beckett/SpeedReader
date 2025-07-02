using System.CommandLine;
using System.Threading.Tasks.Dataflow;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Models;
using Ocr.Blocks;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Core;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Create root command
        var rootCommand = new RootCommand("SpeedReader - Blazing fast OCR");

        // Create process subcommand
        var processCommand = new Command("process", "Process a single image file");

        var inputArgument = new Argument<FileInfo>(
            name: "input",
            description: "Input image file");

        var outputArgument = new Argument<FileInfo?>(
            name: "output",
            description: "Output image file")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var vizOption = new Option<VizMode>(
            name: "--viz",
            description: "Visualization mode",
            getDefaultValue: () => VizMode.Basic);

        processCommand.AddArgument(inputArgument);
        processCommand.AddArgument(outputArgument);
        processCommand.AddOption(vizOption);

        processCommand.SetHandler(async (input, output, vizMode) =>
        {
            await ProcessFile(input, output, vizMode);
        }, inputArgument, outputArgument, vizOption);

        // Create serve subcommand
        var serveCommand = new Command("serve", "Run as HTTP server");

        var portOption = new Option<int>(
            name: "--port",
            description: "Port to listen on",
            getDefaultValue: () => 5000);

        var serveVizOption = new Option<VizMode>(
            name: "--viz",
            description: "Visualization mode for server responses",
            getDefaultValue: () => VizMode.Basic);

        serveCommand.AddOption(portOption);
        serveCommand.AddOption(serveVizOption);

        serveCommand.SetHandler(async (port, vizMode) =>
        {
            await RunServer(port, vizMode);
        }, portOption, serveVizOption);

        // Add subcommands to root
        rootCommand.AddCommand(processCommand);
        rootCommand.AddCommand(serveCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task ProcessFile(FileInfo input, FileInfo? output, VizMode vizMode)
    {
        try
        {
            // Validate input file exists
            if (!input.Exists)
            {
                Console.Error.WriteLine($"Error: Input file '{input.FullName}' not found.");
                Environment.Exit(1);
            }

            // Generate output filename if not specified
            if (output == null)
            {
                var inputDir = Path.GetDirectoryName(input.FullName) ?? ".";
                var inputName = Path.GetFileNameWithoutExtension(input.FullName);
                var inputExt = Path.GetExtension(input.FullName);
                var outputPath = Path.Combine(inputDir, $"{inputName}_viz{inputExt}");
                output = new FileInfo(outputPath);
            }

            // Load input image
            using var image = await Image.LoadAsync<Rgb24>(input.FullName);

            // Create OCR pipeline
            var dbnetSession = ModelZoo.GetInferenceSession(Model.DbNet18);
            var svtrSession = ModelZoo.GetInferenceSession(Model.SVTRv2);

            var ocrBlock = OcrBlock.Create(dbnetSession, svtrSession);

            var results = new List<(Image<Rgb24>, List<Rectangle>, List<string>, VizBuilder)>();
            var resultCollector =
                new ActionBlock<(Image<Rgb24>, List<Rectangle>, List<string>, VizBuilder)>(
                    data => results.Add(data));

            ocrBlock.LinkTo(resultCollector, new DataflowLinkOptions { PropagateCompletion = true });

            // Create VizBuilder and send to pipeline
            var vizBuilder = VizBuilder.Create(vizMode, image);
            await ocrBlock.SendAsync((image, vizBuilder));
            ocrBlock.Complete();
            await resultCollector.Completion;

            // Generate visualization using VizBuilder
            if (vizMode == VizMode.None)
            {
                Console.WriteLine("OCR completed. No visualization generated (--viz None).");
            }
            else
            {
                var result = results[0];
                var outputImage = result.Item4.Render();

                // Save output image
                await outputImage.SaveAsync(output.FullName);
                Console.WriteLine($"OCR visualization saved to: {output.FullName}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task RunServer(int port, VizMode vizMode)
    {
        // Create shared inference sessions
        var dbnetSession = ModelZoo.GetInferenceSession(Model.DbNet18);
        var svtrSession = ModelZoo.GetInferenceSession(Model.SVTRv2);

        // Create minimal web app
        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();

        app.MapPost("/speedread", async context =>
        {
            try
            {
                // Read image from request body
                using var memoryStream = new MemoryStream();
                await context.Request.Body.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // Load image
                using var image = await Image.LoadAsync<Rgb24>(memoryStream);

                // Create per-request OCR pipeline
                var ocrBlock = OcrBlock.Create(dbnetSession, svtrSession);

                var results = new List<(Image<Rgb24>, List<Rectangle>, List<string>, VizBuilder)>();
                var resultCollector =
                    new ActionBlock<(Image<Rgb24>, List<Rectangle>, List<string>, VizBuilder)>(
                        data => results.Add(data));

                ocrBlock.LinkTo(resultCollector, new DataflowLinkOptions { PropagateCompletion = true });

                // Create VizBuilder and send to pipeline
                var vizBuilder = VizBuilder.Create(vizMode, image);
                await ocrBlock.SendAsync((image, vizBuilder));
                ocrBlock.Complete();
                await resultCollector.Completion;

                // Generate visualization
                var result = results[0];
                var outputImage = result.Item4.Render();

                // Return annotated image
                context.Response.ContentType = "image/png";
                await outputImage.SaveAsPngAsync(context.Response.Body);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync($"Error processing image: {ex.Message}");
            }
        });

        var url = $"http://localhost:{port}";
        Console.WriteLine($"Starting SpeedReader server on {url}");
        await app.RunAsync(url);
    }
}
