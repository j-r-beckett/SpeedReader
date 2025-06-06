using Engine;
using Engine.Test;
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
        var modelOutput = ModelRunner.Run(session, preprocessedBuffer.AsTensor());

        // Step 3: Post-processing  
        var detectedPolygons = DBNet.PostProcess(modelOutput, originalWidth, originalHeight);

        // Assert: Verify we detected at least one polygon
        Assert.NotEmpty(detectedPolygons);
        _logger.LogInformation($"Detected {detectedPolygons.Count} text regions");

        // Create visualization with bounding boxes
        using var resultImage = testImage.Clone();

        resultImage.Mutate(ctx =>
        {
            var colors = new[] { Color.Red, Color.Green, Color.Blue, Color.Orange, Color.Purple };

            for (int i = 0; i < detectedPolygons.Count; i++)
            {
                var polygon = detectedPolygons[i];
                var color = colors[i % colors.Length];

                // Draw polygon outline
                var points = polygon.Select(p => new PointF(p.X, p.Y)).ToArray();
                if (points.Length >= 3)
                {
                    ctx.DrawPolygon(Pens.Solid(color, 4), points);
                }

                // Draw bounding rectangle
                var minX = polygon.Min(p => p.X);
                var minY = polygon.Min(p => p.Y);
                var maxX = polygon.Max(p => p.X);
                var maxY = polygon.Max(p => p.Y);
                var boundingRect = new RectangleF(minX, minY, maxX - minX, maxY - minY);
                ctx.Draw(Pens.Dot(color, 2), boundingRect);

                _logger.LogInformation($"Region {i}: Polygon with {polygon.Count} vertices, bounding box: {boundingRect}");
            }
        });

        // Save result image and print URI
        var filename = $"end-to-end-result-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        await _urlPublisher.PublishAsync(resultImage, filename);

        // Verify text was detected in approximately correct location
        var firstPolygon = detectedPolygons[0];
        var detectedCenterX = firstPolygon.Average(p => p.X);
        var detectedCenterY = firstPolygon.Average(p => p.Y);

        // Allow some tolerance for detection accuracy
        var tolerance = Math.Min(originalWidth, originalHeight) * 0.3; // 30% tolerance
        Assert.True(Math.Abs(detectedCenterX - (centerX + textSize.Width / 2)) < tolerance,
            $"Detected text center X ({detectedCenterX:F1}) should be near expected center ({centerX + textSize.Width / 2:F1})");
        Assert.True(Math.Abs(detectedCenterY - (centerY + textSize.Height / 2)) < tolerance,
            $"Detected text center Y ({detectedCenterY:F1}) should be near expected center ({centerY + textSize.Height / 2:F1})");

        _logger.LogInformation($"✓ End-to-end pipeline completed successfully. Expected text center: ({centerX + textSize.Width / 2:F1}, {centerY + textSize.Height / 2:F1}), Detected center: ({detectedCenterX:F1}, {detectedCenterY:F1})");
    }
}
