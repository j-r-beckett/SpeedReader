using System.CommandLine;
using System.Reflection;
using System.Threading.Tasks.Dataflow;
using Models;
using Ocr.Blocks;
using Ocr.Visualization;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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

        var rootCommand = new RootCommand("SpeedReader - Blazing fast OCR")
        {
            inputArgument,
            outputArgument
        };

        rootCommand.SetHandler(async (input, output) =>
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
                    var outputPath = Path.Combine(inputDir, $"{inputName}_ocr{inputExt}");
                    output = new FileInfo(outputPath);
                }

                // Load input image
                using var image = await Image.LoadAsync<Rgb24>(input.FullName);

                // Create OCR pipeline
                var dbnetSession = ModelZoo.GetInferenceSession(Model.DbNet18);
                var svtrSession = ModelZoo.GetInferenceSession(Model.SVTRv2);

                var ocrBlock = OcrBlock.Create(dbnetSession, svtrSession);

                var results = new List<(Image<Rgb24>, List<Rectangle>, List<string>)>();
                var resultCollector =
                    new ActionBlock<(Image<Rgb24>, List<Rectangle>, List<string>, VizBuilder)>(
                        data => results.Add((data.Item1, data.Item2, data.Item3)));

                ocrBlock.LinkTo(resultCollector, new DataflowLinkOptions { PropagateCompletion = true });

                // Create VizBuilder and send to pipeline
                var vizBuilder = VizBuilder.Create(VizMode.None, image);
                await ocrBlock.SendAsync((image, vizBuilder));
                ocrBlock.Complete();
                await resultCollector.Completion;

                // Extract results for annotation
                var detectedRectangles = results.SelectMany(r => r.Item2).ToList();
                var recognizedTexts = results.SelectMany(r => r.Item3).ToList();

                // Step 3: Annotate image with results
                if (detectedRectangles.Count > 0)
                {
                    // Load embedded Arial font
                    var assembly = Assembly.GetExecutingAssembly();
                    await using var fontStream = assembly.GetManifestResourceStream("Core.arial.ttf");
                    var fontCollection = new FontCollection();
                    var fontFamily = fontCollection.Add(fontStream!);
                    var font = fontFamily.CreateFont(14, FontStyle.Bold);

                    image.Mutate(ctx =>
                    {
                        for (int i = 0; i < detectedRectangles.Count; i++)
                        {
                            var rectangle = detectedRectangles[i];

                            // Draw bounding box
                            var boundingRect = new RectangleF(rectangle.X, rectangle.Y, rectangle.Width,
                                rectangle.Height);
                            ctx.Draw(Pens.Solid(Color.Red, 2), boundingRect);

                            // Draw recognized text to the right of the bounding box (if available)
                            if (i < recognizedTexts.Count)
                            {
                                var textPosition = new PointF(rectangle.X + rectangle.Width + 5, rectangle.Y);
                                var text = recognizedTexts[i].Trim();

                                var whiteBrush = Brushes.Solid(Color.White);
                                var blueBrush = Brushes.Solid(Color.Blue);

                                // Draw white outline with improved legibility
                                var outlineWidth = 1;
                                for (int dx = -outlineWidth; dx <= outlineWidth; dx++)
                                {
                                    for (int dy = -outlineWidth; dy <= outlineWidth; dy++)
                                    {
                                        if (dx != 0 || dy != 0)
                                        {
                                            ctx.DrawText(text, font, whiteBrush,
                                                new PointF(textPosition.X + dx, textPosition.Y + dy));
                                        }
                                    }
                                }

                                // Draw blue text on top
                                ctx.DrawText(text, font, blueBrush, textPosition);
                            }
                        }
                    });
                }

                // Save output image
                await image.SaveAsync(output.FullName);

                Console.WriteLine($"OCR results saved to: {output.FullName}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex}");
                Console.Error.WriteLine($"Inner: {ex.InnerException?.StackTrace}");
                Environment.Exit(1);
            }
        }, inputArgument, outputArgument);

        return await rootCommand.InvokeAsync(args);
    }
}
