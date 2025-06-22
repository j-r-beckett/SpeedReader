using System.Diagnostics;
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

public class TextDetection
{
    private readonly Font _font;
    private readonly FileSystemUrlPublisher<TextDetection> _imageSaver;

    public TextDetection(ITestOutputHelper outputHelper)
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
        _imageSaver = new FileSystemUrlPublisher<TextDetection>("/tmp", new TestLogger<TextDetection>(outputHelper));
    }

    [Fact]
    public async Task TextDetectionReturnsCorrectBoundingBoxes()
    {
        const int size = 3;
        var images = new Image<Rgb24>[size];
        var boundingBoxes = new List<Rectangle>[size];
        for (int i = 0; i < boundingBoxes.Length; i++)
        {
            boundingBoxes[i] = [];
        }

        images[0] = new Image<Rgb24>(1200, 800, Color.White);
        boundingBoxes[0].Add(DrawText(images[0], "hello", 100, 100));
        boundingBoxes[0].Add(DrawText(images[0], "world", 300, 150));
        boundingBoxes[0].Add(DrawText(images[0], "quick", 800, 100));
        boundingBoxes[0].Add(DrawText(images[0], "brown", 1000, 200));
        boundingBoxes[0].Add(DrawText(images[0], "fox", 150, 500));
        boundingBoxes[0].Add(DrawText(images[0], "jumps", 250, 650));
        boundingBoxes[0].Add(DrawText(images[0], "over", 900, 550));
        boundingBoxes[0].Add(DrawText(images[0], "lazy", 800, 700));
        boundingBoxes[0].Add(DrawText(images[0], "dog", 600, 400));

        images[1] = new Image<Rgb24>(1200, 800, Color.White);
        boundingBoxes[1].Add(DrawText(images[1], "top", 600, 2));
        boundingBoxes[1].Add(DrawText(images[1], "right", 1150, 400));
        boundingBoxes[1].Add(DrawText(images[1], "bottom", 600, 780));
        boundingBoxes[1].Add(DrawText(images[1], "left", 2, 400));
        boundingBoxes[1].Add(DrawText(images[1], "topleft", 2, 2));
        boundingBoxes[1].Add(DrawText(images[1], "topright", 1120, 2));
        boundingBoxes[1].Add(DrawText(images[1], "bottomright", 1090, 780));
        boundingBoxes[1].Add(DrawText(images[1], "bottomleft", 2, 780));

        images[2] = new Image<Rgb24>(1200, 800, Color.White);
        boundingBoxes[2].Add(DrawText(images[2], "hello", 100, 100));
        boundingBoxes[2].Add(DrawText(images[2], "world", 200, 100));
        boundingBoxes[2].Add(DrawText(images[2], "hello", 100, 200));
        boundingBoxes[2].Add(DrawText(images[2], "world", 175, 200));
        boundingBoxes[2].Add(DrawText(images[2], "hello", 100, 300));
        boundingBoxes[2].Add(DrawText(images[2], "world", 150, 300));
        boundingBoxes[2].Add(DrawText(images[2], "goodbye", 500, 100));
        boundingBoxes[2].Add(DrawText(images[2], "planet", 500, 200));
        boundingBoxes[2].Add(DrawText(images[2], "goodbye", 600, 100));
        boundingBoxes[2].Add(DrawText(images[2], "planet", 600, 150));
        boundingBoxes[2].Add(DrawText(images[2], "goodbye", 700, 100));
        boundingBoxes[2].Add(DrawText(images[2], "planet", 700, 125));

        await TestTextDetection(images, boundingBoxes);
    }

    [Fact]
    public async Task TextDetectionHandlesJaggedBatch()
    {
        const int size = 3;
        var images = new Image<Rgb24>[size];
        var boundingBoxes = new List<Rectangle>[size];
        for (int i = 0; i < boundingBoxes.Length; i++)
        {
            boundingBoxes[i] = [];
        }

        // Square image
        images[0] = new Image<Rgb24>(800, 800, Color.White);
        boundingBoxes[0].Add(DrawText(images[0], "square", 100, 100));
        boundingBoxes[0].Add(DrawText(images[0], "center", 350, 350));
        boundingBoxes[0].Add(DrawText(images[0], "corner", 650, 650));
        boundingBoxes[0].Add(DrawText(images[0], "side", 100, 650));
        boundingBoxes[0].Add(DrawText(images[0], "edge", 650, 100));

        // Long and short rectangle
        images[1] = new Image<Rgb24>(2400, 400, Color.White);
        boundingBoxes[1].Add(DrawText(images[1], "wide", 100, 100));
        boundingBoxes[1].Add(DrawText(images[1], "long", 800, 200));
        boundingBoxes[1].Add(DrawText(images[1], "stretch", 2000, 100));
        boundingBoxes[1].Add(DrawText(images[1], "span", 400, 300));
        boundingBoxes[1].Add(DrawText(images[1], "broad", 1200, 300));

        // Tall and thin rectangle
        images[2] = new Image<Rgb24>(400, 1600, Color.White);
        boundingBoxes[2].Add(DrawText(images[2], "tall", 100, 100));
        boundingBoxes[2].Add(DrawText(images[2], "thin", 200, 400));
        boundingBoxes[2].Add(DrawText(images[2], "high", 100, 800));
        boundingBoxes[2].Add(DrawText(images[2], "long", 200, 1200));
        boundingBoxes[2].Add(DrawText(images[2], "slim", 100, 1500));

        await TestTextDetection(images, boundingBoxes);
    }

    private async Task TestTextDetection(Image<Rgb24>[] images, List<Rectangle>[] expectedBoundingBoxes)
    {
        Debug.Assert(images.Length == expectedBoundingBoxes.Length);

        var actualBoundingBoxes = RunTextDetection(images);

        for (int i = 0; i < images.Length; i++)
        {
            await SaveDebugImage(images[i], expectedBoundingBoxes[i], actualBoundingBoxes[i]);
        }

        Assert.Equal(expectedBoundingBoxes.Length, actualBoundingBoxes.Length);

        for (int i = 0; i < expectedBoundingBoxes.Length; i++)
        {
            Assert.Equal(expectedBoundingBoxes[i].Count, actualBoundingBoxes[i].Count);

            var actuals = actualBoundingBoxes[i].ToList();
            for (int j = 0; j < expectedBoundingBoxes[i].Count; j++)
            {
                var expected = expectedBoundingBoxes[i][j];
                var closestActual = actuals.MinBy(r => CalculateCloseness(r, expected));
                actuals.Remove(closestActual);
                Assert.True(Pad(closestActual, 2).Contains(expected));

                Assert.True(closestActual.Width * 0.5 < expected.Width);
                Assert.True(closestActual.Height * 0.5 < expected.Height);
            }
        }
    }

    private static Rectangle Pad(Rectangle r, int p) => new(r.X - p, r.Y - p, r.Width + 2 * p, r.Height + 2 * p);

    private static int CalculateCloseness(Rectangle r1, Rectangle r2)
    {
        return EuclideanDistance(GetMidpoint(r1), GetMidpoint(r2));

        (int X, int Y) GetMidpoint(Rectangle r) => ((r.Left + r.Right) / 2, r.Top + r.Bottom / 2);

        int EuclideanDistance((int X, int Y) p1, (int X, int Y) p2) =>
            (int)Math.Ceiling(Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2)));
    }

    private static List<Rectangle>[] RunTextDetection(Image<Rgb24>[] images)
    {
        var tensor = DBNet.PreProcess(images).AsTensor();
        var rawResults = ModelRunner.Run(ModelZoo.GetInferenceSession(Model.DbNet18), tensor);
        return DBNet.PostProcess(rawResults, images);
    }

    private Rectangle DrawText(Image image, string text, int x, int y)
    {
        image.Mutate(ctx => ctx.DrawText(text, _font, Color.Black, new PointF(x, y)));
        var textRect = TextMeasurer.MeasureAdvance(text, new TextOptions(_font));
        return new Rectangle(x, y, (int)Math.Ceiling(textRect.Width), (int)Math.Ceiling(textRect.Height));
    }

    private async Task SaveDebugImage(Image originalImage, IEnumerable<Rectangle> expectedBoundingBoxes, IEnumerable<Rectangle> actualBoundingBoxes)
    {
        var debugImage = originalImage.Clone(ctx =>
        {
            foreach (var bbox in expectedBoundingBoxes)
            {
                ctx.Draw(Pens.Dash(Color.Green, 1), bbox);
            }
            foreach (var bbox in actualBoundingBoxes)
            {
                ctx.Draw(Pens.Solid(Color.Red, 1), bbox);
            }
        });
        await _imageSaver.PublishAsync(debugImage);
    }
}
