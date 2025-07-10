using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Models;
using Ocr.Blocks;
using Ocr.Visualization;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Video;
using Video.Test;
using Xunit.Abstractions;

namespace Ocr.Test.E2E;

public class OcrPipelineE2ETests
{
    private readonly Font _font;
    private readonly FileSystemUrlPublisher<OcrPipelineE2ETests> _imageSaver;

    public OcrPipelineE2ETests(ITestOutputHelper outputHelper)
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

        _font = fontFamily.CreateFont(18);
        _imageSaver = new FileSystemUrlPublisher<OcrPipelineE2ETests>("/tmp", new TestLogger<OcrPipelineE2ETests>(outputHelper));
    }

    private async Task TestOcrPipeline(Image<Rgb24>[] images, List<Rectangle>[] expectedBoundingBoxes, List<string>[] expectedTexts)
    {
        Debug.Assert(images.Length == expectedBoundingBoxes.Length);
        Debug.Assert(images.Length == expectedTexts.Length);
        for (int i = 0; i < images.Length; i++)
        {
            Debug.Assert(expectedBoundingBoxes[i].Count == expectedTexts[i].Count);
        }

        var pipelineResults = await RunOcrPipeline(images);

        // Save debug images first (before assertions)
        for (int i = 0; i < images.Length; i++)
        {
            await SaveDebugImage(images[i], expectedBoundingBoxes[i], pipelineResults[i]);
        }

        // Assert we got the right number of results for each image
        Assert.Equal(images.Length, pipelineResults.Count);

        // For each image, validate detection and recognition
        for (int i = 0; i < images.Length; i++)
        {
            var actualBoundingBoxes = pipelineResults[i].Select(r => r.Box).ToList();
            var actualTexts = pipelineResults[i].Select(r => r.Text).ToList();

            // Assert detection accuracy (using existing smart logic)
            AssertDetectionAccuracy(expectedBoundingBoxes[i], actualBoundingBoxes);

            // Assert recognition accuracy (exact match after trimming)
            AssertRecognitionAccuracy(expectedBoundingBoxes[i], expectedTexts[i], pipelineResults[i]);
        }
    }

    private void AssertDetectionAccuracy(List<Rectangle> expectedBoxes, List<Rectangle> actualBoxes)
    {
        Assert.Equal(expectedBoxes.Count, actualBoxes.Count);

        var remainingActuals = actualBoxes.ToList();
        foreach (var expected in expectedBoxes)
        {
            var closestActual = remainingActuals.MinBy(r => CalculateCloseness(r, expected));
            remainingActuals.Remove(closestActual);

            // Check that the actual box contains the expected box (with padding tolerance)
            Assert.True(Pad(closestActual, 2).Contains(expected));

            // Check that the actual box is a reasonable size relative to expected
            Assert.True(closestActual.Width * 0.5 < expected.Width);
            Assert.True(closestActual.Height * 0.5 < expected.Height);
        }
    }

    private void AssertRecognitionAccuracy(List<Rectangle> expectedBoxes, List<string> expectedTexts, List<(Rectangle Box, string Text)> actualResults)
    {
        var remainingActuals = actualResults.ToList();

        for (int i = 0; i < expectedBoxes.Count; i++)
        {
            var expected = expectedBoxes[i];
            var expectedText = expectedTexts[i];

            // Find the closest actual result
            var closestActual = remainingActuals.MinBy(r => CalculateCloseness(r.Box, expected));
            remainingActuals.Remove(closestActual);

            // Assert text matches exactly (after trimming)
            Assert.Equal(expectedText.Trim(), closestActual.Text.Trim());
        }
    }

    private async Task<List<List<(Rectangle Box, string Text)>>> RunOcrPipeline(Image<Rgb24>[] images)
    {
        using var dbnetSession = ModelZoo.GetInferenceSession(Model.DbNet18);
        using var svtrSession = ModelZoo.GetInferenceSession(Model.SVTRv2);

        // Create pipeline without post-processing merging
        var dbNetBlock = DBNetBlock.Create(dbnetSession);
        var svtrBlock = SVTRBlock.Create(svtrSession);

        // Use a dictionary to maintain order
        var resultsDict = new Dictionary<Image<Rgb24>, List<(Rectangle Box, string Text)>>();
        var resultsLock = new object();

        var resultCollector = new ActionBlock<(Image<Rgb24>, List<TextBoundary>, List<string>, VizBuilder)>(data =>
        {
            var imageResults = new List<(Rectangle Box, string Text)>();

            // Filter out empty text but don't merge
            for (int i = 0; i < data.Item2.Count; i++)
            {
                var cleanedText = new string(data.Item3[i].Where(c => c <= 127).ToArray()).Trim();
                if (!string.IsNullOrEmpty(cleanedText))
                {
                    imageResults.Add((data.Item2[i].AARectangle, cleanedText));
                }
            }

            lock (resultsLock)
            {
                resultsDict[data.Item1] = imageResults;
            }
        });

        dbNetBlock.LinkTo(svtrBlock, new DataflowLinkOptions { PropagateCompletion = true });
        svtrBlock.LinkTo(resultCollector, new DataflowLinkOptions { PropagateCompletion = true });

        // Send all images through the pipeline
        foreach (var image in images)
        {
            // Create VizBuilder and send to pipeline (same pattern as CLI)
            var vizBuilder = VizBuilder.Create(VizMode.None, image);
            await dbNetBlock.SendAsync((image, vizBuilder));
        }

        dbNetBlock.Complete();
        await resultCollector.Completion;

        // Return results in the same order as input images
        var orderedResults = new List<List<(Rectangle Box, string Text)>>();
        foreach (var image in images)
        {
            orderedResults.Add(resultsDict[image]);
        }

        return orderedResults;
    }

    private Rectangle DrawText(Image image, string text, int x, int y)
    {
        image.Mutate(ctx => ctx.DrawText(text, _font, Color.Black, new PointF(x, y)));
        var textRect = TextMeasurer.MeasureAdvance(text, new TextOptions(_font));
        return new Rectangle(x, y, (int)Math.Ceiling(textRect.Width), (int)Math.Ceiling(textRect.Height));
    }

    private async Task SaveDebugImage(Image originalImage, List<Rectangle> expectedBoundingBoxes, List<(Rectangle Box, string Text)> actualResults)
    {
        var debugImage = originalImage.Clone(ctx =>
        {
            // Draw expected boxes in green dashed lines
            foreach (var bbox in expectedBoundingBoxes)
            {
                ctx.Draw(Pens.Dash(Color.Green, 1), bbox);
            }

            // Draw actual boxes in red solid lines with recognized text
            foreach (var (box, text) in actualResults)
            {
                ctx.Draw(Pens.Solid(Color.Red, 1), box);

                // Draw recognized text below the bounding box
                var labelY = box.Bottom + 5;
                ctx.DrawText(text, _font.Family.CreateFont(12), Color.Blue, new PointF(box.X, labelY));
            }
        });
        await _imageSaver.PublishAsync(debugImage);
    }

    private static Rectangle Pad(Rectangle r, int p) => new(r.X - p, r.Y - p, r.Width + 2 * p, r.Height + 2 * p);

    private static int CalculateCloseness(Rectangle r1, Rectangle r2)
    {
        return EuclideanDistance(GetMidpoint(r1), GetMidpoint(r2));

        (int X, int Y) GetMidpoint(Rectangle r) => ((r.Left + r.Right) / 2, (r.Top + r.Bottom) / 2);

        int EuclideanDistance((int X, int Y) p1, (int X, int Y) p2) =>
            (int)Math.Ceiling(Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2)));
    }

    [Fact]
    public async Task BasicTextLayout()
    {
        const int size = 3;
        var images = new Image<Rgb24>[size];
        var boundingBoxes = new List<Rectangle>[size];
        var expectedTexts = new List<string>[size];
        for (int i = 0; i < size; i++)
        {
            boundingBoxes[i] = [];
            expectedTexts[i] = [];
        }

        // First image - basic scattered text
        images[0] = new Image<Rgb24>(1200, 800, Color.White);
        boundingBoxes[0].Add(DrawText(images[0], "hello", 100, 100));
        expectedTexts[0].Add("hello");
        boundingBoxes[0].Add(DrawText(images[0], "world", 300, 150));
        expectedTexts[0].Add("world");
        boundingBoxes[0].Add(DrawText(images[0], "quick", 800, 100));
        expectedTexts[0].Add("quick");
        boundingBoxes[0].Add(DrawText(images[0], "brown", 1000, 200));
        expectedTexts[0].Add("brown");
        boundingBoxes[0].Add(DrawText(images[0], "fox", 150, 500));
        expectedTexts[0].Add("fox");
        boundingBoxes[0].Add(DrawText(images[0], "jumps", 250, 650));
        expectedTexts[0].Add("jumps");
        boundingBoxes[0].Add(DrawText(images[0], "over", 900, 550));
        expectedTexts[0].Add("over");
        boundingBoxes[0].Add(DrawText(images[0], "lazy", 800, 700));
        expectedTexts[0].Add("lazy");
        boundingBoxes[0].Add(DrawText(images[0], "dog", 600, 400));
        expectedTexts[0].Add("dog");

        // Second image - edge positions
        images[1] = new Image<Rgb24>(1200, 800, Color.White);
        boundingBoxes[1].Add(DrawText(images[1], "top", 600, 2));
        expectedTexts[1].Add("top");
        boundingBoxes[1].Add(DrawText(images[1], "right", 1150, 400));
        expectedTexts[1].Add("right");
        boundingBoxes[1].Add(DrawText(images[1], "bottom", 600, 780));
        expectedTexts[1].Add("bottom");
        boundingBoxes[1].Add(DrawText(images[1], "left", 2, 400));
        expectedTexts[1].Add("left");
        boundingBoxes[1].Add(DrawText(images[1], "topleft", 2, 2));
        expectedTexts[1].Add("topleft");
        boundingBoxes[1].Add(DrawText(images[1], "topright", 1120, 2));
        expectedTexts[1].Add("topright");
        boundingBoxes[1].Add(DrawText(images[1], "bottomright", 1090, 780));
        expectedTexts[1].Add("bottomright");
        boundingBoxes[1].Add(DrawText(images[1], "bottomleft", 2, 780));
        expectedTexts[1].Add("bottomleft");

        // Third image - adjacent text
        images[2] = new Image<Rgb24>(1200, 800, Color.White);
        boundingBoxes[2].Add(DrawText(images[2], "hello", 100, 100));
        expectedTexts[2].Add("hello");
        boundingBoxes[2].Add(DrawText(images[2], "world", 200, 100));
        expectedTexts[2].Add("world");
        boundingBoxes[2].Add(DrawText(images[2], "hello", 100, 200));
        expectedTexts[2].Add("hello");
        boundingBoxes[2].Add(DrawText(images[2], "world", 175, 200));
        expectedTexts[2].Add("world");
        boundingBoxes[2].Add(DrawText(images[2], "hello", 100, 300));
        expectedTexts[2].Add("hello");
        boundingBoxes[2].Add(DrawText(images[2], "world", 150, 300));
        expectedTexts[2].Add("world");
        boundingBoxes[2].Add(DrawText(images[2], "goodbye", 500, 100));
        expectedTexts[2].Add("goodbye");
        boundingBoxes[2].Add(DrawText(images[2], "planet", 500, 200));
        expectedTexts[2].Add("planet");
        boundingBoxes[2].Add(DrawText(images[2], "goodbye", 600, 100));
        expectedTexts[2].Add("goodbye");
        boundingBoxes[2].Add(DrawText(images[2], "planet", 600, 150));
        expectedTexts[2].Add("planet");
        boundingBoxes[2].Add(DrawText(images[2], "goodbye", 700, 100));
        expectedTexts[2].Add("goodbye");
        boundingBoxes[2].Add(DrawText(images[2], "planet", 700, 125));
        expectedTexts[2].Add("planet");

        await TestOcrPipeline(images, boundingBoxes, expectedTexts);
    }
}
