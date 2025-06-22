using System.CommandLine;
using System.Reflection;
using System.Threading.Tasks.Dataflow;
using Models;
using Ocr.Blocks;
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

        var outputArgument = new Argument<FileInfo>(
            name: "output",
            description: "Output image file",
            getDefaultValue: () => new FileInfo("out.png"))
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var rootCommand = new RootCommand("SpeedReader - Text detection tool")
        {
            inputArgument,
            outputArgument
        };

        rootCommand.SetHandler(async (FileInfo input, FileInfo output) =>
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


                // Create OCR pipeline
                using var dbnetSession = ModelZoo.GetInferenceSession(Model.DbNet18);
                using var svtrSession = ModelZoo.GetInferenceSession(Model.SVTRv2);

                var ocrBlock = OcrBlock.Create(dbnetSession, svtrSession);

                var results = new List<(Image<Rgb24>, List<Rectangle>, List<string>)>();
                var resultCollector = new ActionBlock<(Image<Rgb24>, List<Rectangle>, List<string>)>(data => results.Add(data));

                ocrBlock.LinkTo(resultCollector, new DataflowLinkOptions { PropagateCompletion = true });

                await ocrBlock.SendAsync(image);
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
                    using var fontStream = assembly.GetManifestResourceStream("Core.arial.ttf");
                    var fontCollection = new FontCollection();
                    var fontFamily = fontCollection.Add(fontStream!);
                    var font = fontFamily.CreateFont(16, FontStyle.Bold);

                    image.Mutate(ctx =>
                    {
                        for (int i = 0; i < detectedRectangles.Count; i++)
                        {
                            var rectangle = detectedRectangles[i];

                            // Draw bounding box
                            var boundingRect = new RectangleF(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
                            ctx.Draw(Pens.Solid(Color.Red, 2), boundingRect);

                            // Draw recognized text above the bounding box (if available)
                            if (i < recognizedTexts.Count)
                            {
                                var text = recognizedTexts[i].Trim();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    var textPosition = new PointF(rectangle.X, Math.Max(0, rectangle.Y - 20));
                                    ctx.DrawText(text, font, Color.Blue, textPosition);
                                }
                            }
                        }
                    });

                    Console.WriteLine($"Detected {detectedRectangles.Count} text regions");
                    if (recognizedTexts.Count > 0)
                    {
                        Console.WriteLine($"Recognized {recognizedTexts.Count} text segments:");
                        for (int i = 0; i < recognizedTexts.Count; i++)
                        {
                            Console.WriteLine($"  {i + 1}: '{recognizedTexts[i].Trim()}'");
                        }
                    }
                }

                // Save output image
                await image.SaveAsync(output.FullName);
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
