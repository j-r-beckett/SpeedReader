using System.CommandLine;
using System.Threading.Tasks.Dataflow;
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
        // string ffmpegPath = FFmpegResolver.GetFFmpegPath();
        // Console.WriteLine($"FFmpeg path: {ffmpegPath}");

        var inputArgument = new Argument<FileInfo>(
            name: "input",
            description: "Input image file")
        {
            Arity = ArgumentArity.ExactlyOne
        };

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

        var rootCommand = new RootCommand("SpeedReader - Blazing fast OCR")
        {
            inputArgument,
            outputArgument,
            vizOption
        };

        rootCommand.SetHandler(async (input, output, vizMode) =>
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
                Console.Error.WriteLine($"Error: {ex}");
                Console.Error.WriteLine($"Inner: {ex.InnerException?.StackTrace}");
                Environment.Exit(1);
            }
        }, inputArgument, outputArgument, vizOption);

        return await rootCommand.InvokeAsync(args);
    }
}
