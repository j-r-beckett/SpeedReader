using Microsoft.Extensions.Logging;
using Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Video;
using Video.Test;
using Xunit.Abstractions;

namespace Ocr.Test;

[Collection("ONNX")]
public class EndToEndTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly TestLogger<EndToEndTests> _logger;
    private readonly FileSystemUrlPublisher<EndToEndTests> _urlPublisher;
    private readonly Font _font;

    public EndToEndTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _logger = new TestLogger<EndToEndTests>(outputHelper);
        var outputDirectory = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "out", "debug");
        _urlPublisher = new FileSystemUrlPublisher<EndToEndTests>(outputDirectory, _logger);

        // Load font for text rendering
        FontFamily fontFamily;
        if (!SystemFonts.TryGet("Arial", out fontFamily))
        {
            fontFamily = SystemFonts.Collection.Families.First();
        }
        _font = fontFamily.CreateFont(48, FontStyle.Regular);
    }

    [Fact]
    public async Task CompleteTextDetectionPipeline_DetectsTextAndDrawsBoundingBoxes()
    {
        // Arrange: Create test image with single word
        const string testWord = "HELLO";
        using var testImage = new Image<Rgb24>(800, 600, new Rgb24(255, 255, 255)); // White background

        // Draw text in center
        var textSize = TextMeasurer.MeasureSize(testWord, new TextOptions(_font));
        var centerX = (testImage.Width - textSize.Width) / 2;
        var centerY = (testImage.Height - textSize.Height) / 2;
        testImage.Mutate(ctx => ctx.DrawText(testWord, _font, new Rgb24(0, 0, 0), new PointF(centerX, centerY)));

        // Store original dimensions
        int originalWidth = testImage.Width;
        int originalHeight = testImage.Height;

        // Act: Run complete pipeline
        using var session = ModelZoo.GetInferenceSession(Model.DbNet18);

        // Step 1: Preprocessing
        using var preprocessedBuffer = DBNet.PreProcess([testImage]);

        // Step 2: Inference
        using var modelOutput = ModelRunner.Run(session, preprocessedBuffer.AsTensor());

        // Step 3: Post-processing  
        var detectedPolygons = DBNet.PostProcess(modelOutput, originalWidth, originalHeight);

        // Assert: Verify we detected at least one rectangle in first batch
        Assert.NotEmpty(detectedPolygons);
        Assert.NotEmpty(detectedPolygons[0]);
        _logger.LogInformation($"Detected {detectedPolygons[0].Count} text regions in first batch");

        // Create visualization with bounding boxes
        using var resultImage = testImage.Clone();

        resultImage.Mutate(ctx =>
        {
            var colors = new[] { Color.Red, Color.Green, Color.Blue, Color.Orange, Color.Purple };
            var firstBatchRectangles = detectedPolygons[0];

            for (int i = 0; i < firstBatchRectangles.Count; i++)
            {
                var rectangle = firstBatchRectangles[i];
                var color = colors[i % colors.Length];

                // Draw bounding rectangle
                var boundingRect = new RectangleF(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
                ctx.Draw(Pens.Solid(color, 4), boundingRect);

                _logger.LogInformation($"Region {i}: Rectangle {boundingRect}");
            }
        });

        // Save result image and print URI
        var filename = $"end-to-end-result-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        await _urlPublisher.PublishAsync(resultImage, filename);

        // Verify text was detected in approximately correct location
        var firstRectangle = detectedPolygons[0][0];
        var detectedCenterX = firstRectangle.X + firstRectangle.Width / 2.0;
        var detectedCenterY = firstRectangle.Y + firstRectangle.Height / 2.0;

        // Allow some tolerance for detection accuracy
        var tolerance = Math.Min(originalWidth, originalHeight) * 0.3; // 30% tolerance
        Assert.True(Math.Abs(detectedCenterX - (centerX + textSize.Width / 2)) < tolerance,
            $"Detected text center X ({detectedCenterX:F1}) should be near expected center ({centerX + textSize.Width / 2:F1})");
        Assert.True(Math.Abs(detectedCenterY - (centerY + textSize.Height / 2)) < tolerance,
            $"Detected text center Y ({detectedCenterY:F1}) should be near expected center ({centerY + textSize.Height / 2:F1})");

        _logger.LogInformation($"✓ End-to-end pipeline completed successfully. Expected text center: ({centerX + textSize.Width / 2:F1}, {centerY + textSize.Height / 2:F1}), Detected center: ({detectedCenterX:F1}, {detectedCenterY:F1})");
    }
}
