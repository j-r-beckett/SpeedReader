using Engine;
using Engine.Test;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit.Abstractions;

namespace TextDetection.Test;

public class TextDetectorTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly TestLogger<TextDetectorTests> _logger;
    private readonly FileSystemUrlPublisher<TextDetectorTests> _urlPublisher;
    private readonly Font _font;

    public TextDetectorTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _logger = new TestLogger<TextDetectorTests>(outputHelper);
        var outputDirectory = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "out", "debug");
        _urlPublisher = new FileSystemUrlPublisher<TextDetectorTests>(outputDirectory, _logger);

        // Load font for text rendering
        FontFamily fontFamily;
        if (!SystemFonts.TryGet("Arial", out fontFamily))
        {
            fontFamily = SystemFonts.Collection.Families.First();
        }
        _font = fontFamily.CreateFont(48, FontStyle.Regular);
    }

    [Fact]
    public async Task SmokeTest_DetectTextInGeneratedImage()
    {
        // Create test image with "hello" text
        using var testImage = new Image<Rgb24>(800, 600, new Rgb24(255, 255, 255));

        testImage.Mutate(ctx => ctx
            .DrawText(
                "hello",
                _font,
                new Rgb24(0, 0, 0),
                new PointF(350, 275) // Center the text
            )
        );

        // Save original test image
        await _urlPublisher.PublishAsync(testImage, "test-input.png");
        _logger.LogInformation("Original test image saved");

        // Convert to DBNetImage
        var dbnetImage = DbNetImage.Create(testImage);

        // Create TextDetector
        using var session = ModelZoo.GetInferenceSession(Model.DbNet18);
        using var detector = new TextDetector(session, new TestLogger<TextDetector>(_outputHelper));

        // Create input tensor
        using var input = new TextDetectorInput(1, dbnetImage.Height, dbnetImage.Width);
        input.LoadBatch(dbnetImage);

        // Run text detection
        var output = detector.RunTextDetection(input);

        // Convert result to greyscale image
        using var resultImage = output.RenderAsGreyscale();

        // Save result image
        await _urlPublisher.PublishAsync(resultImage, "detection-result.png");
        _logger.LogInformation("Detection result saved");

        // Basic assertions
        output.Should().NotBeNull();
        output.ProbabilityMap.Should().NotBeNull();
        output.ProbabilityMap.GetLength(0).Should().BeGreaterThan(0);
        output.ProbabilityMap.GetLength(1).Should().BeGreaterThan(0);

        // Check that some probabilities are greater than 0 (indicating potential text detection)
        bool hasPositiveProbabilities = false;
        int height = output.ProbabilityMap.GetLength(0);
        int width = output.ProbabilityMap.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (output.ProbabilityMap[y, x] > 0.1f)
                {
                    hasPositiveProbabilities = true;
                    break;
                }
            }
            if (hasPositiveProbabilities) break;
        }

        hasPositiveProbabilities.Should().BeTrue("Expected some text detection probabilities > 0.1");

        _logger.LogInformation("Smoke test completed successfully");
    }

    [Fact]
    public async Task DetectionAccuracy_ValidatesPixelLevelAccuracy()
    {
        var random = new Random(0); // Deterministic seed
        using var session = ModelZoo.GetInferenceSession(Model.DbNet18);
        using var detector = new TextDetector(session, new TestLogger<TextDetector>(_outputHelper));

        for (int iteration = 0; iteration < 10; iteration++)
        {
            _logger.LogInformation("Starting accuracy test iteration {Iteration}", iteration + 1);

            // Create test canvas
            using var testImage = new Image<Rgb24>(1920, 1080, new Rgb24(255, 255, 255));
            var wordBounds = new List<RectangleF>();

            // Generate 5 words (colors will be assigned in visualization)
            int wordCount = 5;
            for (int i = 0; i < wordCount; i++)
            {
                // Generate random string (5-10 characters)
                int stringLength = random.Next(5, 11);
                string randomText = GenerateRandomString(random, stringLength);

                // Find random position that fits the text
                var textSize = TextMeasurer.MeasureSize(randomText, new TextOptions(_font));
                float x = random.Next(0, (int)(1920 - textSize.Width));
                float y = random.Next(0, (int)(1080 - textSize.Height));
                var position = new PointF(x, y);

                // Draw the text
                testImage.Mutate(ctx => ctx.DrawText(randomText, _font, new Rgb24(0, 0, 0), position));

                // Store bounding box for validation
                wordBounds.Add(new RectangleF(x, y, textSize.Width, textSize.Height));
            }

            // Store original dimensions before DbNetImage.Create() mutates the image
            int originalWidth = testImage.Width;
            int originalHeight = testImage.Height;

            // Convert to DBNetImage and run detection
            var dbnetImage = DbNetImage.Create(testImage);
            using var input = new TextDetectorInput(1, dbnetImage.Height, dbnetImage.Width);
            input.LoadBatch(dbnetImage);
            var output = detector.RunTextDetection(input);

            // Get model output dimensions
            int modelHeight = output.ProbabilityMap.GetLength(0);
            int modelWidth = output.ProbabilityMap.GetLength(1);

            // Validate detection accuracy and create visualization in model coordinate space
            using var visualizationImage = ValidateAndVisualize(testImage, wordBounds, output, originalWidth, originalHeight, modelWidth, modelHeight, iteration);
            await _urlPublisher.PublishAsync(visualizationImage, $"accuracy-test-{iteration}-visualization.png");
        }

        _logger.LogInformation("All accuracy test iterations completed successfully");
    }

    private static string GenerateRandomString(Random random, int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }


    private Image<Rgb24> ValidateAndVisualize(Image<Rgb24> processedImage, List<RectangleF> wordBounds, TextDetectorOutput output, int originalWidth, int originalHeight, int modelWidth, int modelHeight, int iteration)
    {
        // Define distinctive colors for word identification with names
        var wordColorsWithNames = new (Color color, string name)[]
        {
            (Color.Red, "Red"),
            (Color.Green, "Green"),
            (Color.Blue, "Blue"),
            (Color.Orange, "Orange"),
            (Color.Purple, "Purple")
        };

        // Calculate scale factors to convert from original to model coordinates
        float scaleX = (float)modelWidth / originalWidth;
        float scaleY = (float)modelHeight / originalHeight;

        // Create a side-by-side visualization in model coordinate space
        int visualWidth = modelWidth + modelWidth;
        int visualHeight = modelHeight;

        var visualization = new Image<Rgb24>(visualWidth, visualHeight, new Rgb24(128, 128, 128)); // Gray background

        // Draw processed image on the left (scale DOWN to model size)
        var scaledInputImage = processedImage.Clone();
        scaledInputImage.Mutate(ctx => ctx.Resize(modelWidth, modelHeight));
        visualization.Mutate(ctx => ctx.DrawImage(scaledInputImage, new Point(0, 0), 1.0f));

        // Draw detection result on the right (already at model size)
        using var detectionImage = output.RenderAsGreyscale();
        visualization.Mutate(ctx => ctx.DrawImage(detectionImage, new Point(modelWidth, 0), 1.0f));

        // Convert word bounds to model coordinate space
        var scaledWordBounds = wordBounds.Select(bound => new RectangleF(
            bound.X * scaleX,
            bound.Y * scaleY,
            bound.Width * scaleX,
            bound.Height * scaleY
        )).ToList();

        // Collect validation results to assert after visualization is published
        var validationResults = new List<(int wordIndex, string colorName, float averageProbability)>();

        // Overlay word bounding boxes
        visualization.Mutate(ctx =>
        {
            // Colored boxes on processed image (left side) - scaled word bounds
            for (int wordIndex = 0; wordIndex < scaledWordBounds.Count; wordIndex++)
            {
                var scaledBound = scaledWordBounds[wordIndex];
                var wordColor = wordColorsWithNames[wordIndex].color;
                ctx.Draw(Pens.Solid(wordColor, 4), scaledBound);
            }

            // Colored boxes on detection result (right side) - show buffers for sampling logic
            for (int wordIndex = 0; wordIndex < scaledWordBounds.Count; wordIndex++)
            {
                var scaledBound = scaledWordBounds[wordIndex];
                var (wordColor, colorName) = wordColorsWithNames[wordIndex];
                var offsetBound = new RectangleF(
                    scaledBound.X + modelWidth,
                    scaledBound.Y,
                    scaledBound.Width,
                    scaledBound.Height);

                // Original bounding box
                ctx.Draw(Pens.Solid(wordColor, 4), offsetBound);

                // Outer buffer - used for background pixel counting
                var outerBuffer = new RectangleF(
                    offsetBound.X - 10,
                    offsetBound.Y - 10,
                    offsetBound.Width + 20,
                    offsetBound.Height + 20);
                ctx.Draw(Pens.Dot(wordColor, 4), outerBuffer);

                // Inner buffer - used for text pixel counting
                var innerBuffer = new RectangleF(
                    offsetBound.X + 10,
                    offsetBound.Y + 10,
                    offsetBound.Width - 20,
                    offsetBound.Height - 20);
                if (innerBuffer.Width > 0 && innerBuffer.Height > 0)
                {
                    ctx.Draw(Pens.Dot(wordColor, 4), innerBuffer);

                    // Validate this word's detection accuracy using model coordinates
                    float totalProbability = 0;
                    int pixelCount = 0;

                    // Sample from the inner buffer in model coordinate space
                    int startY = Math.Max(0, (int)Math.Floor(scaledBound.Y + 10));
                    int endY = Math.Min(modelHeight - 1, (int)Math.Ceiling(scaledBound.Y + scaledBound.Height - 10));
                    int startX = Math.Max(0, (int)Math.Floor(scaledBound.X + 10));
                    int endX = Math.Min(modelWidth - 1, (int)Math.Ceiling(scaledBound.X + scaledBound.Width - 10));

                    for (int y = startY; y <= endY; y++)
                    {
                        for (int x = startX; x <= endX; x++)
                        {
                            totalProbability += output.ProbabilityMap[y, x];  // row, col
                            pixelCount++;
                        }
                    }

                    if (pixelCount > 0)
                    {
                        float averageProbability = totalProbability / pixelCount;
                        _logger.LogInformation($"Word {wordIndex} ({colorName}): Average probability in inner buffer = {averageProbability:F3} (sampled {pixelCount} pixels)");

                        // Collect validation result for later assertion
                        validationResults.Add((wordIndex, colorName, averageProbability));
                    }
                }
            }
        });

        // Perform validation assertions after visualization is created
        foreach (var (wordIndex, colorName, averageProbability) in validationResults)
        {
            Assert.True(averageProbability > 0.7f,
                $"Word {wordIndex} ({colorName}) has low average probability {averageProbability:F3} in inner buffer. Expected > 0.7");
        }

        return visualization;
    }

}
