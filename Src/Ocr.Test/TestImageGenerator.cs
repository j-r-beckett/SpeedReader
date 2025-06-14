using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;

namespace Ocr.Test;

public record RenderedText(string Text);
public record GeneratedImage(Image<Rgb24> Image, RenderedText[] RenderedTexts);

public static class TestImageGenerator
{
    private static readonly char[] UppercaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

    public static GeneratedImage Generate(Size imageSize, params Rectangle[] hintBoxes)
    {
        var image = new Image<Rgb24>(imageSize.Width, imageSize.Height, Color.White);
        var renderedTexts = new RenderedText[hintBoxes.Length];
        var random = new Random(0);
        var fontFamily = GetFontFamily();

        for (int i = 0; i < hintBoxes.Length; i++)
        {
            renderedTexts[i] = RenderTextInHintBox(image, hintBoxes[i], fontFamily, random);
        }

        return new GeneratedImage(image, renderedTexts);
    }

    private static RenderedText RenderTextInHintBox(Image<Rgb24> image, Rectangle hintBox, FontFamily fontFamily, Random random)
    {
        // With monospace font, calculate optimal character count first
        var maxWidth = hintBox.Width * 0.9f;
        var maxHeight = hintBox.Height * 0.8f;

        // Start with font size based on height constraint
        var fontSize = maxHeight;
        var font = fontFamily.CreateFont(fontSize, FontStyle.Regular);
        
        // Measure a single character width (monospace = all chars same width)
        var singleCharWidth = TextMeasurer.MeasureSize("A", new TextOptions(font)).Width;
        
        // Calculate how many characters can fit
        var targetLength = Math.Max(2, Math.Min(8, (int)(maxWidth / singleCharWidth)));
        var text = GenerateRandomLetters(random, targetLength);

        // Safety check: ensure text won't overshoot the box
        var testTextSize = TextMeasurer.MeasureSize(text, new TextOptions(font));
        while (testTextSize.Width > maxWidth && targetLength > 2)
        {
            targetLength--;
            text = GenerateRandomLetters(random, targetLength);
            testTextSize = TextMeasurer.MeasureSize(text, new TextOptions(font));
        }

        // Now size font to fit both width and height constraints
        fontSize = Math.Min(maxWidth / text.Length, maxHeight);
        font = fontFamily.CreateFont(fontSize, FontStyle.Regular);
        var textSize = TextMeasurer.MeasureSize(text, new TextOptions(font));

        // Ensure text actually fits, reduce font size if needed
        while ((textSize.Width > maxWidth || textSize.Height > maxHeight) && fontSize > 8)
        {
            fontSize *= 0.9f;
            font = fontFamily.CreateFont(fontSize, FontStyle.Regular);
            textSize = TextMeasurer.MeasureSize(text, new TextOptions(font));
        }

        // Center text in hint box
        var x = hintBox.X + (hintBox.Width - textSize.Width) / 2;
        var y = hintBox.Y + (hintBox.Height - textSize.Height) / 2;

        // Draw hint box and text (no ActualBounds needed)
        image.Mutate(ctx =>
        {
            ctx.Draw(Pens.Solid(Color.LightGray, 2), hintBox);
            ctx.DrawText(text, font, Color.Black, new PointF(x, y));
        });

        return new RenderedText(text);
    }

    private static string GenerateRandomLetters(Random random, int length)
    {
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = UppercaseLetters[random.Next(UppercaseLetters.Length)];
        }
        return new string(chars);
    }

    private static FontFamily GetFontFamily()
    {
        // Try to get a monospace font first for consistent character spacing
        if (SystemFonts.TryGet("Courier New", out var courier))
            return courier;
        if (SystemFonts.TryGet("Consolas", out var consolas))
            return consolas;
        if (SystemFonts.TryGet("Monaco", out var monaco))
            return monaco;
        
        // Fallback to Arial if no monospace fonts available
        return SystemFonts.TryGet("Arial", out var arial) ? arial : SystemFonts.Families.First();
    }
}

public static class TextBoxLayouts
{
    public static Rectangle[] CreateGrid(int rows, int cols, int boxWidth, int boxHeight, int margin = 20, int startX = 20, int startY = 20)
    {
        var boxes = new List<Rectangle>();

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                var x = startX + col * (boxWidth + margin);
                var y = startY + row * (boxHeight + margin);
                boxes.Add(new Rectangle(x, y, boxWidth, boxHeight));
            }
        }

        return boxes.ToArray();
    }

    public static Size CalculateImageSize(int rows, int cols, int boxWidth, int boxHeight, int margin = 20, int padding = 40)
    {
        var width = cols * boxWidth + (cols - 1) * margin + padding;
        var height = rows * boxHeight + (rows - 1) * margin + padding;
        return new Size(width, height);
    }
}
