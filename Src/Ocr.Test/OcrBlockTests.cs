using System.Threading.Tasks.Dataflow;
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
public class OcrBlockTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly TestLogger<OcrBlockTests> _logger;
    private readonly FileSystemUrlPublisher<OcrBlockTests> _urlPublisher;
    private readonly Font _font;

    public OcrBlockTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _logger = new TestLogger<OcrBlockTests>(outputHelper);
        var outputDirectory = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "out", "debug");
        _urlPublisher = new FileSystemUrlPublisher<OcrBlockTests>(outputDirectory, _logger);

        // Load font for text rendering
        FontFamily fontFamily;
        if (!SystemFonts.TryGet("Arial", out fontFamily))
        {
            fontFamily = SystemFonts.Collection.Families.First();
        }
        _font = fontFamily.CreateFont(48, FontStyle.Regular);
    }

    [Fact]
    public async Task OcrBlock_ProcessesSingleImage_ReturnsDetectedTextRegions()
    {
        // Arrange: Create test image with text
        const string testWord = "OCR TEST";
        using var testImage = new Image<Rgb24>(640, 480, new Rgb24(255, 255, 255)); // White background

        // Draw text in center
        var textSize = TextMeasurer.MeasureSize(testWord, new TextOptions(_font));
        var centerX = (testImage.Width - textSize.Width) / 2;
        var centerY = (testImage.Height - textSize.Height) / 2;
        testImage.Mutate(ctx => ctx.DrawText(testWord, _font, new Rgb24(0, 0, 0), new PointF(centerX, centerY)));

        // Create OCR block
        using var session = ModelZoo.GetInferenceSession(Model.DbNet18);
        var ocrBlock = OcrBlock.CreateOcrBlock(session);

        // Act: Send image through pipeline and collect results
        var results = new List<List<Rectangle>>();
        var actionBlock = new ActionBlock<List<Rectangle>>(result => results.Add(result));

        ocrBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });

        await ocrBlock.SendAsync(testImage);
        ocrBlock.Complete();

        await ocrBlock.Completion;
        await actionBlock.Completion;

        // Assert: Verify we received results
        Assert.NotEmpty(results);
        Assert.NotEmpty(results[0]);
        _logger.LogInformation($"OCR Block detected {results[0].Count} text regions");

        // Create visualization with bounding boxes
        using var resultImage = testImage.Clone();
        resultImage.Mutate(ctx =>
        {
            var rectangle = results[0][0];
            var boundingRect = new RectangleF(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
            ctx.Draw(Pens.Solid(Color.Red, 4), boundingRect);
            _logger.LogInformation($"Detected region: {boundingRect}");
        });

        // Save result image
        var filename = $"ocr-block-result-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        await _urlPublisher.PublishAsync(resultImage, filename);

        // Verify text was detected in approximately correct location
        var firstRectangle = results[0][0];
        var detectedCenterX = firstRectangle.X + firstRectangle.Width / 2.0;
        var detectedCenterY = firstRectangle.Y + firstRectangle.Height / 2.0;

        // Allow some tolerance for detection accuracy
        var tolerance = Math.Min(testImage.Width, testImage.Height) * 0.3; // 30% tolerance
        Assert.True(Math.Abs(detectedCenterX - (centerX + textSize.Width / 2)) < tolerance,
            $"Detected text center X ({detectedCenterX:F1}) should be near expected center ({centerX + textSize.Width / 2:F1})");
        Assert.True(Math.Abs(detectedCenterY - (centerY + textSize.Height / 2)) < tolerance,
            $"Detected text center Y ({detectedCenterY:F1}) should be near expected center ({centerY + textSize.Height / 2:F1})");

        _logger.LogInformation($"✓ OCR Block pipeline completed successfully. Expected center: ({centerX + textSize.Width / 2:F1}, {centerY + textSize.Height / 2:F1}), Detected center: ({detectedCenterX:F1}, {detectedCenterY:F1})");
    }
}
