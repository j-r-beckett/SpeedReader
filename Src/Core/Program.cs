using System.CommandLine;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Models;
using Ocr;
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

        var homepageOption = new Option<FileInfo?>(
            name: "--homepage",
            description: "HTML file to serve at root path");

        serveCommand.AddOption(homepageOption);

        serveCommand.SetHandler(async (homepage) =>
        {
            await RunServer(homepage);
        }, homepageOption);

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

            var results = new List<(Image<Rgb24>, OcrResult, VizBuilder)>();
            var resultCollector =
                new ActionBlock<(Image<Rgb24>, OcrResult, VizBuilder)>(
                    data => results.Add(data));

            ocrBlock.LinkTo(resultCollector, new DataflowLinkOptions { PropagateCompletion = true });

            // Create VizBuilder and send to pipeline
            var vizBuilder = VizBuilder.Create(vizMode, image);
            await ocrBlock.SendAsync((image, vizBuilder));
            ocrBlock.Complete();
            await resultCollector.Completion;

            // Get results
            var result = results[0];

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
            Environment.Exit(1);
        }
    }

    private static async Task RunServer(FileInfo? homepage)
    {
        // Create shared inference sessions
        var dbnetSession = ModelZoo.GetInferenceSession(Model.DbNet18);
        var svtrSession = ModelZoo.GetInferenceSession(Model.SVTRv2);

        // Create minimal web app
        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();

        // Serve homepage at root if specified
        if (homepage != null)
        {
            if (!homepage.Exists)
            {
                Console.Error.WriteLine($"Error: Homepage file '{homepage.FullName}' not found.");
                Environment.Exit(1);
            }

            app.MapGet("/", async context =>
            {
                context.Response.ContentType = "text/html";
                await context.Response.SendFileAsync(homepage.FullName);
            });
        }

        app.MapGet("/health", () => "Healthy");

        app.MapPost("api/ocr", async context =>
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

                var results = new List<(Image<Rgb24>, OcrResult, VizBuilder)>();
                var resultCollector =
                    new ActionBlock<(Image<Rgb24>, OcrResult, VizBuilder)>(
                        data => results.Add(data));

                ocrBlock.LinkTo(resultCollector, new DataflowLinkOptions { PropagateCompletion = true });

                // Create VizBuilder and send to pipeline (always VizMode.None for server)
                var vizBuilder = VizBuilder.Create(VizMode.None, image);
                await ocrBlock.SendAsync((image, vizBuilder));
                ocrBlock.Complete();
                await resultCollector.Completion;

                // Get results
                var result = results[0];

                // Update page number
                var ocrResults = result.Item2 with { PageNumber = 0 };

                // Return JSON response
                context.Response.ContentType = "application/json";
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(ocrResults, jsonOptions);
                await context.Response.WriteAsync(json);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync($"Error processing image: {ex.Message}");
            }
        });

        Console.WriteLine("Starting SpeedReader server...");
        await app.RunAsync();
    }
}
