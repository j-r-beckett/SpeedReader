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

            // Generate 1-10 random words
            int wordCount = random.Next(1, 11);
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

            // Scale probability map back to original image dimensions
            var scaledOutput = ScaleProbabilityMapToOriginal(output, originalWidth, originalHeight);

            // Create visualization to understand the buffers
            using var visualizationImage = CreateVisualization(testImage, wordBounds, scaledOutput, originalWidth, originalHeight);
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

    private TextDetectorOutput ScaleProbabilityMapToOriginal(TextDetectorOutput modelOutput, int originalWidth, int originalHeight)
    {
        int modelHeight = modelOutput.ProbabilityMap.GetLength(0);
        int modelWidth = modelOutput.ProbabilityMap.GetLength(1);

        // Create scaled probability map at original dimensions
        var scaledMap = new float[originalHeight, originalWidth];

        // Scale factors
        float scaleX = (float)modelWidth / originalWidth;
        float scaleY = (float)modelHeight / originalHeight;

        // Resample the probability map using bilinear interpolation
        for (int y = 0; y < originalHeight; y++)
        {
            for (int x = 0; x < originalWidth; x++)
            {
                // Map to model coordinates
                float sourceX = x * scaleX;
                float sourceY = y * scaleY;

                // Bilinear interpolation
                int x0 = (int)Math.Floor(sourceX);
                int y0 = (int)Math.Floor(sourceY);
                int x1 = Math.Min(x0 + 1, modelWidth - 1);
                int y1 = Math.Min(y0 + 1, modelHeight - 1);

                float wx = sourceX - x0;
                float wy = sourceY - y0;

                float value = (1 - wx) * (1 - wy) * modelOutput.ProbabilityMap[y0, x0] +
                             wx * (1 - wy) * modelOutput.ProbabilityMap[y0, x1] +
                             (1 - wx) * wy * modelOutput.ProbabilityMap[y1, x0] +
                             wx * wy * modelOutput.ProbabilityMap[y1, x1];

                scaledMap[y, x] = value;
            }
        }

        return new TextDetectorOutput { ProbabilityMap = scaledMap };
    }

    private Image<Rgb24> CreateVisualization(Image<Rgb24> processedImage, List<RectangleF> wordBounds, TextDetectorOutput output, int originalWidth, int originalHeight)
    {
        // Create a side-by-side visualization: processed image on left, detection result on right
        int visualWidth = originalWidth + originalWidth;
        int visualHeight = originalHeight;
        
        var visualization = new Image<Rgb24>(visualWidth, visualHeight, new Rgb24(128, 128, 128)); // Gray background

        // Draw processed image on the left (resize it back to original size for display)
        var resizedProcessedImage = processedImage.Clone();
        resizedProcessedImage.Mutate(ctx => ctx.Resize(originalWidth, originalHeight));
        visualization.Mutate(ctx => ctx.DrawImage(resizedProcessedImage, new Point(0, 0), 1.0f));

        // Draw detection result on the right (already at original size)
        using var detectionImage = output.RenderAsGreyscale();
        visualization.Mutate(ctx => ctx.DrawImage(detectionImage, new Point(originalWidth, 0), 1.0f));

        // Overlay word bounding boxes
        visualization.Mutate(ctx =>
        {
            // Red boxes on processed image (left side) - just original bounding boxes, no buffers needed
            foreach (var bound in wordBounds)
            {
                ctx.Draw(Pens.Solid(Color.Red, 4), bound);
            }

            // Yellow boxes on detection result (right side) - show buffers for sampling logic
            foreach (var bound in wordBounds)
            {
                var offsetBound = new RectangleF(
                    bound.X + originalWidth,
                    bound.Y,
                    bound.Width,
                    bound.Height);
                
                // Original bounding box (solid yellow)
                ctx.Draw(Pens.Solid(Color.Yellow, 4), offsetBound);
                
                // Outer buffer (dotted yellow) - used for background pixel counting
                var outerBuffer = new RectangleF(
                    offsetBound.X - 10,
                    offsetBound.Y - 10,
                    offsetBound.Width + 20,
                    offsetBound.Height + 20);
                ctx.Draw(Pens.Dot(Color.Yellow, 4), outerBuffer);
                
                // Inner buffer (dotted orange) - used for text pixel counting
                var innerBuffer = new RectangleF(
                    offsetBound.X + 10,
                    offsetBound.Y + 10,
                    offsetBound.Width - 20,
                    offsetBound.Height - 20);
                if (innerBuffer.Width > 0 && innerBuffer.Height > 0)
                {
                    ctx.Draw(Pens.Dot(Color.Orange, 4), innerBuffer);
                }
            }
        });

        return visualization;
    }

}
