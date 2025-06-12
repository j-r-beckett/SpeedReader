using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;
using Video;
using Video.Test;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Ocr.Test;

public class OcrValidator
{
    private readonly ITestOutputHelper? _testOutputHelper;
    private readonly FileSystemUrlPublisher<object>? _urlPublisher;

    public OcrValidator(ITestOutputHelper? testOutputHelper = null)
    {
        _testOutputHelper = testOutputHelper;
        if (testOutputHelper != null)
        {
            var logger = new TestLogger<object>(testOutputHelper);
            var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "out", "debug");
            _urlPublisher = new FileSystemUrlPublisher<object>(outputDirectory, logger);
        }
    }

    public void ValidateOcrResults(GeneratedImage generated, string[] recognizedTexts, Rectangle[] detectedBoxes, Rectangle[] hintBoxes)
    {
        // Generate debug image first for debugging failures
        if (_urlPublisher != null)
        {
            GenerateDebugImage(generated, recognizedTexts, detectedBoxes, hintBoxes);
        }

        // Validate counts
        Assert.Equal(detectedBoxes.Length, recognizedTexts.Length);
        Assert.Equal(generated.RenderedTexts.Length, hintBoxes.Length);
        Assert.Equal(generated.RenderedTexts.Length, detectedBoxes.Length);

        // Find spatial matching between detected boxes and hint boxes
        var matches = FindSpatialMatches(hintBoxes, detectedBoxes);
        Assert.True(matches != null, 
            $"Could not create spatial matching between {detectedBoxes.Length} detected boxes and {hintBoxes.Length} hint boxes. " +
            "This usually means detected boxes don't overlap sufficiently with hint boxes.");

        // Validate each matched pair for both text and spatial accuracy
        for (int i = 0; i < recognizedTexts.Length; i++)
        {
            var recognizedText = recognizedTexts[i].Trim();
            var detectedBox = detectedBoxes[i];
            var matchedHintIndex = matches[i];
            var expectedText = generated.RenderedTexts[matchedHintIndex].Text.Trim();
            var hintBox = hintBoxes[matchedHintIndex];

            // Text validation: Check if recognized text matches expected text
            Assert.True(TextMatches(expectedText, recognizedText),
                $"Text mismatch at index {i}: expected '{expectedText}', got '{recognizedText}'");

            // Spatial validation: Check if detected box is within hint box
            Assert.True(IsContainedWithin(detectedBox, hintBox),
                $"Detected box {detectedBox} at index {i} is not contained within hint box {hintBox}");

            var minWidth = hintBox.Width * 0.6;
            var minHeight = hintBox.Height * 0.4;
            Assert.True(MeetsMinimumSize(detectedBox, hintBox),
                $"Detected box {detectedBox} at index {i} is too small (min: {minWidth:F0}x{minHeight:F0})");
        }
    }


    private bool TextMatches(string expected, string actual)
    {
        // First try exact match
        if (expected == actual)
            return true;

        // Try with common OCR character substitutions
        var normalizedExpected = NormalizeOcrText(expected);
        var normalizedActual = NormalizeOcrText(actual);

        return normalizedExpected == normalizedActual;
    }

    private string NormalizeOcrText(string text)
    {
        return text
            .Replace('O', '0')  // Letter O → Digit 0
            .Replace('o', '0')  // Lowercase o → Digit 0
            .Replace('I', '1')  // Letter I → Digit 1
            .Replace('l', '1')  // Lowercase l → Digit 1
            .Replace('S', '5')  // Letter S → Digit 5
            .Replace('s', '5')  // Lowercase s → Digit 5
            .Replace('Z', '2')  // Letter Z → Digit 2
            .Replace('z', '2')  // Lowercase z → Digit 2
            .Replace('G', '6')  // Letter G → Digit 6
            .Replace('g', '6')  // Lowercase g → Digit 6
            .Replace('B', '8')  // Letter B → Digit 8
            .Replace('b', '8')  // Lowercase b → Digit 8
            .ToUpperInvariant(); // Normalize case
    }


    private int[]? FindSpatialMatches(Rectangle[] hintBoxes, Rectangle[] detectedBoxes)
    {
        // Returns array where matches[i] = index of hint box that detected box i should match to
        // Returns null if no valid bijective mapping can be found

        var matches = new int[detectedBoxes.Length];
        var usedHints = new bool[hintBoxes.Length];

        // For each detected box, find the best matching hint box
        for (int detectedIndex = 0; detectedIndex < detectedBoxes.Length; detectedIndex++)
        {
            var detected = detectedBoxes[detectedIndex];
            int bestHintIndex = -1;
            double bestOverlap = 0;

            // Find hint box with highest overlap
            for (int hintIndex = 0; hintIndex < hintBoxes.Length; hintIndex++)
            {
                if (usedHints[hintIndex])
                    continue; // Already matched

                var hint = hintBoxes[hintIndex];
                var overlap = CalculateOverlapArea(detected, hint);

                if (overlap > bestOverlap)
                {
                    bestOverlap = overlap;
                    bestHintIndex = hintIndex;
                }
            }

            // Must have some overlap to be a valid match
            if (bestHintIndex == -1 || bestOverlap <= 0)
                return null;

            matches[detectedIndex] = bestHintIndex;
            usedHints[bestHintIndex] = true;
        }

        return matches;
    }

    private double CalculateOverlapArea(Rectangle a, Rectangle b)
    {
        var intersection = Rectangle.Intersect(a, b);
        return intersection.IsEmpty ? 0.0 : intersection.Width * intersection.Height;
    }


    private bool IsContainedWithin(Rectangle inner, Rectangle outer)
    {
        return inner.X >= outer.X &&
               inner.Y >= outer.Y &&
               inner.Right <= outer.Right &&
               inner.Bottom <= outer.Bottom;
    }

    private bool MeetsMinimumSize(Rectangle detected, Rectangle hint)
    {
        var minWidth = hint.Width * 0.6;   // 40% minimum width
        var minHeight = hint.Height * 0.4; // 30% minimum height

        return detected.Width >= minWidth && detected.Height >= minHeight;
    }

    private void GenerateDebugImage(GeneratedImage generated, string[] recognizedTexts, Rectangle[] detectedBoxes, Rectangle[] hintBoxes)
    {
        using var debugImage = generated.Image.Clone();
        var fontFamily = SystemFonts.TryGet("Arial", out var arial) ? arial : SystemFonts.Families.First();
        var font = fontFamily.CreateFont(14);

        debugImage.Mutate(ctx =>
        {
            // Draw hint boxes in gray
            foreach (var hint in hintBoxes)
            {
                ctx.Draw(Pens.Solid(Color.LightGray, 2), hint);
            }

            // Draw detected boxes in red and add recognized text labels
            for (int i = 0; i < detectedBoxes.Length; i++)
            {
                var detected = detectedBoxes[i];
                ctx.Draw(Pens.Solid(Color.Red, 2), detected);

                // Draw recognized text above the detected box if available
                if (i < recognizedTexts.Length && !string.IsNullOrEmpty(recognizedTexts[i]))
                {
                    var textPosition = new PointF(detected.X, detected.Y - 18);
                    ctx.DrawText(recognizedTexts[i], font, Color.Blue, textPosition);
                }
            }
        });

        var filename = $"ocr-validation-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png";
        _urlPublisher!.PublishAsync(debugImage, filename).Wait();
    }
}
