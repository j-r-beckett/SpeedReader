using System.CommandLine;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Models;
using Ocr.Blocks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Core;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
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

        var rootCommand = new RootCommand("Wheft - Text detection tool")
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


                // Create inference session for DBNet
                using var session = ModelZoo.GetInferenceSession(Model.DbNet18);

                // Create DBNetBlock
                var dbNetBlock = DBNetBlock.Create(session);

                // Create action block to collect results
                var results = new List<List<Rectangle>>();
                var actionBlock = new ActionBlock<List<Rectangle>>(result => results.Add(result));

                // Link blocks
                dbNetBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });

                // Send image through pipeline
                await dbNetBlock.SendAsync(image);
                dbNetBlock.Complete();

                // Wait for completion
                await dbNetBlock.Completion;
                await actionBlock.Completion;

                // Draw bounding boxes on the image
                if (results.Count > 0 && results[0].Count > 0)
                {
                    image.Mutate(ctx =>
                    {
                        foreach (var rectangle in results[0])
                        {
                            var boundingRect = new RectangleF(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
                            ctx.Draw(Pens.Solid(Color.Red, 3), boundingRect);
                        }
                    });
                }

                // Save output image
                await image.SaveAsync(output.FullName);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }, inputArgument, outputArgument);

        return await rootCommand.InvokeAsync(args);
    }
}
