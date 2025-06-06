using Video;
using Video.Test;
using Microsoft.Extensions.Logging;
using Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit.Abstractions;

namespace OCR.Test;

[Collection("ONNX")]
public class TextDetectorTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly TestLogger<TextDetectorTests> _logger;
    private readonly FileSystemUrlPublisher<TextDetectorTests> _urlPublisher;
    private readonly Font _font;
    private readonly int _bufferSize = 10;

    public TextDetectorTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _logger = new TestLogger<TextDetectorTests>(outputHelper);
        var outputDirectory = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "out", "debug");
        _urlPublisher = new FileSystemUrlPublisher<TextDetectorTests>(outputDirectory, _logger);

        _logger.LogError("Hello world");

        // Load font for text rendering
        try
        {
            FontFamily fontFamily;
            if (!SystemFonts.TryGet("Arial", out fontFamily))
            {
                fontFamily = SystemFonts.Collection.Families.First();
            }

            _font = fontFamily.CreateFont(48, FontStyle.Regular);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Font loading failed: {ex}");
            throw;
        }
    }

    [Fact]
    public async Task DetectionAccuracy_ValidatesPixelLevelAccuracy()
    {
        var random = new Random(0); // Deterministic seed
        using var session = ModelZoo.GetInferenceSession(Model.DbNet18);

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

            // Use new 3-class flow: Preprocessor → TextDetector → PostProcessor
            using var preprocessedBuffer = DBNet.PreProcess([testImage]);
            using var modelOutput = ModelRunner.Run(session, preprocessedBuffer.AsTensor());
            var probabilityMaps = TensorTestUtils.ExtractProbabilityMapsForTesting(modelOutput);
            var probabilityMap = probabilityMaps[0];

            // Get model output dimensions
            int modelHeight = probabilityMap.GetLength(0);
            int modelWidth = probabilityMap.GetLength(1);

            // Validate detection accuracy and create visualization in model coordinate space
            using var visualizationImage = ValidateAndVisualize(testImage, wordBounds, probabilityMap, originalWidth, originalHeight, modelWidth, modelHeight, iteration);
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


    private Image<Rgb24> ValidateAndVisualize(Image<Rgb24> processedImage, List<RectangleF> wordBounds, float[,] probabilityMap, int originalWidth, int originalHeight, int modelWidth, int modelHeight, int iteration)
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
        using var detectionImage = RenderAsGreyscale(probabilityMap);
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

                // Buffer - used for text pixel counting
                var buffer = new RectangleF(
                    offsetBound.X + _bufferSize,
                    offsetBound.Y + _bufferSize,
                    offsetBound.Width - 2 * _bufferSize,
                    offsetBound.Height - 2 * _bufferSize);
                if (buffer.Width > 0 && buffer.Height > 0)
                {
                    ctx.Draw(Pens.Dot(wordColor, 4), buffer);

                    // Validate this word's detection accuracy using model coordinates
                    float totalProbability = 0;
                    int pixelCount = 0;

                    // Sample from the buffer in model coordinate space
                    int startY = Math.Max(0, (int)Math.Floor(scaledBound.Y + _bufferSize));
                    int endY = Math.Min(modelHeight - 1, (int)Math.Ceiling(scaledBound.Y + scaledBound.Height - _bufferSize));
                    int startX = Math.Max(0, (int)Math.Floor(scaledBound.X + _bufferSize));
                    int endX = Math.Min(modelWidth - 1, (int)Math.Ceiling(scaledBound.X + scaledBound.Width - _bufferSize));

                    for (int y = startY; y <= endY; y++)
                    {
                        for (int x = startX; x <= endX; x++)
                        {
                            totalProbability += probabilityMap[y, x];  // row, col
                            pixelCount++;
                        }
                    }

                    if (pixelCount > 0)
                    {
                        float averageProbability = totalProbability / pixelCount;
                        _logger.LogInformation($"Word {wordIndex} ({colorName}): Average probability in buffer = {averageProbability:F3} (sampled {pixelCount} pixels)");

                        // Collect validation result for later assertion
                        validationResults.Add((wordIndex, colorName, averageProbability));
                    }
                }
            }
        });

        // Validate background areas (outside all word buffers) have low probability
        float backgroundProbabilityTotal = 0f;
        int backgroundPixelCount = 0;

        // Create buffer regions for all words in model coordinate space
        var buffers = scaledWordBounds.Select(bound => new RectangleF(
            bound.X + _bufferSize,
            bound.Y + _bufferSize,
            bound.Width - 2 * _bufferSize,
            bound.Height - 2 * _bufferSize
        )).Where(buffer => buffer.Width > 0 && buffer.Height > 0).ToList();

        // Scan entire probability map
        for (int y = 0; y < modelHeight; y++)
        {
            for (int x = 0; x < modelWidth; x++)
            {
                // Check if this pixel is inside any word's buffer
                bool insideAnyBuffer = false;
                foreach (var buffer in buffers)
                {
                    if (x >= buffer.Left && x < buffer.Right && y >= buffer.Top && y < buffer.Bottom)
                    {
                        insideAnyBuffer = true;
                        break;
                    }
                }

                // If outside all buffers, it's background
                if (!insideAnyBuffer)
                {
                    backgroundProbabilityTotal += probabilityMap[y, x];
                    backgroundPixelCount++;
                }
            }
        }

        if (backgroundPixelCount > 0)
        {
            float averageBackgroundProbability = backgroundProbabilityTotal / backgroundPixelCount;
            _logger.LogInformation($"Background average probability = {averageBackgroundProbability:F3} (sampled {backgroundPixelCount} pixels outside all word buffers)");

            Assert.True(averageBackgroundProbability < 0.01f,
                $"Background has high average probability {averageBackgroundProbability:F3}. Expected < 0.01 to ensure model isn't just outputting high probability everywhere");
        }

        foreach (var (wordIndex, colorName, averageProbability) in validationResults)
        {
            Assert.True(averageProbability > 0.7f,
                $"Word {wordIndex} ({colorName}) has low average probability {averageProbability:F3} in buffer. Expected > 0.7");
        }

        return visualization;
    }


    private static double CalculateCorrelation(float[,] map1, float[,] map2)
    {
        int height = map1.GetLength(0);
        int width = map1.GetLength(1);

        // Calculate means
        double mean1 = 0, mean2 = 0;
        int count = height * width;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                mean1 += map1[y, x];
                mean2 += map2[y, x];
            }
        }
        mean1 /= count;
        mean2 /= count;

        // Calculate correlation coefficient
        double numerator = 0, sumSq1 = 0, sumSq2 = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double diff1 = map1[y, x] - mean1;
                double diff2 = map2[y, x] - mean2;
                numerator += diff1 * diff2;
                sumSq1 += diff1 * diff1;
                sumSq2 += diff2 * diff2;
            }
        }

        double denominator = Math.Sqrt(sumSq1 * sumSq2);
        return denominator == 0 ? 0 : numerator / denominator;
    }

    private static Image<Rgb24> RenderAsGreyscale(float[,] probabilityMap)
    {
        int height = probabilityMap.GetLength(0);
        int width = probabilityMap.GetLength(1);

        var image = new Image<Rgb24>(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float probability = probabilityMap[y, x];
                byte greyValue = (byte)(probability * 255f);
                image[x, y] = new Rgb24(greyValue, greyValue, greyValue);
            }
        }

        return image;
    }
}
