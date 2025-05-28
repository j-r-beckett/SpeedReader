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
        var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "out", "debug");
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
}
