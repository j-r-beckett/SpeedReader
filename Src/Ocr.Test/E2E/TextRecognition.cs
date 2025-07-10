using System.Diagnostics;
using System.Numerics.Tensors;
using Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Video;
using Video.Test;
using Xunit.Abstractions;

namespace Ocr.Test.E2E;

public class TextRecognition
{
    private readonly Font _font;
    private readonly FileSystemUrlPublisher<TextRecognition> _imageSaver;

    public TextRecognition(ITestOutputHelper outputHelper)
    {
        if (!SystemFonts.TryGet("Arial", out var fontFamily))
        {

            var defaultFontFamily = SystemFonts.Families.FirstOrDefault();
            if (defaultFontFamily != default)
            {
                fontFamily = defaultFontFamily;
            }
            else
            {
                throw new Exception("Failed to load font");
            }
        }

        _font = fontFamily.CreateFont(12);
        _imageSaver = new FileSystemUrlPublisher<TextRecognition>("/tmp", new TestLogger<TextRecognition>(outputHelper));
    }

    [Fact]
    public async Task TextRecognitionHandlesBasicText()
    {
        var testWords = new[] { "quick", "brown", "fox", "jumps", "over", "lazy", "dog", "alphabet" };

        var image = new Image<Rgb24>(1000, 600, Color.White);
        var boundingBoxes = new List<Rectangle>();
        var expectedTexts = new List<string>();

        // Sparse placement with varied positions
        boundingBoxes.Add(DrawText(image, testWords[0], 50, 50));    // quick
        expectedTexts.Add(testWords[0]);

        boundingBoxes.Add(DrawText(image, testWords[1], 700, 80));   // brown
        expectedTexts.Add(testWords[1]);

        boundingBoxes.Add(DrawText(image, testWords[2], 300, 200));  // fox
        expectedTexts.Add(testWords[2]);

        boundingBoxes.Add(DrawText(image, testWords[3], 150, 350));  // jumps
        expectedTexts.Add(testWords[3]);

        boundingBoxes.Add(DrawText(image, testWords[4], 800, 300));  // over
        expectedTexts.Add(testWords[4]);

        boundingBoxes.Add(DrawText(image, testWords[5], 400, 500));  // lazy
        expectedTexts.Add(testWords[5]);

        boundingBoxes.Add(DrawText(image, testWords[6], 100, 520));  // dog
        expectedTexts.Add(testWords[6]);

        boundingBoxes.Add(DrawText(image, testWords[7], 650, 450));  // alphabet
        expectedTexts.Add(testWords[7]);

        await TestTextRecognition(image, boundingBoxes, expectedTexts);
    }


    private async Task TestTextRecognition(Image<Rgb24> image, List<Rectangle> boundingBoxes, List<string> expectedTexts)
    {
        Debug.Assert(boundingBoxes.Count == expectedTexts.Count);

        var actualTexts = RunTextRecognition(image, boundingBoxes);

        await SaveDebugImage(image, boundingBoxes, actualTexts);

        Assert.Equal(expectedTexts.Count, actualTexts.Length);

        for (int i = 0; i < expectedTexts.Count; i++)
        {
            Assert.Equal(expectedTexts[i], actualTexts[i]);
        }
    }

    private static string[] RunTextRecognition(Image<Rgb24> image, List<Rectangle> boundingBoxes)
    {
        // Convert rectangles to TextBoundary objects
        var textBoundaries = boundingBoxes.Select(r => {
            var polygon = new List<(int X, int Y)>
            {
                (r.X, r.Y), (r.Right, r.Y), (r.Right, r.Bottom), (r.X, r.Bottom)
            };
            return TextBoundary.Create(polygon);
        }).ToList();
        
        // Use individual preprocessing
        var processedRegions = SVTRv2.PreProcess(image, textBoundaries);

        // Create tensor and run model
        int numRectangles = boundingBoxes.Count;
        var inputTensor = Tensor.Create(processedRegions, [numRectangles, 3, 48, 320]);
        var rawResult = ModelRunner.Run(ModelZoo.GetInferenceSession(Model.SVTRv2), inputTensor);

        // Extract output data and use individual postprocessing
        var outputData = rawResult.AsSpan().ToArray();
        var results = SVTRv2.PostProcess(outputData, numRectangles);

        rawResult.Dispose();
        return results;
    }

    private Rectangle DrawText(Image image, string text, int x, int y)
    {
        image.Mutate(ctx => ctx.DrawText(text, _font, Color.Black, new PointF(x, y)));
        var textRect = TextMeasurer.MeasureAdvance(text, new TextOptions(_font));
        return new Rectangle(x, y, (int)Math.Ceiling(textRect.Width), (int)Math.Ceiling(textRect.Height));
    }

    private async Task SaveDebugImage(Image originalImage, IEnumerable<Rectangle> boundingBoxes, string[] recognizedTexts)
    {
        var debugImage = originalImage.Clone(ctx =>
        {
            int index = 0;
            foreach (var bbox in boundingBoxes)
            {
                ctx.Draw(Pens.Solid(Color.Red, 2), bbox);

                // Draw recognized text below the bounding box
                if (index < recognizedTexts.Length)
                {
                    var labelY = bbox.Bottom + 5;
                    ctx.DrawText(recognizedTexts[index], _font, Color.Blue, new PointF(bbox.X, labelY));
                }
                index++;
            }
        });
        await _imageSaver.PublishAsync(debugImage);
    }
}
