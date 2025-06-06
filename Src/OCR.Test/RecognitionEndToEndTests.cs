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
public class RecognitionEndToEndTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly TestLogger<RecognitionEndToEndTests> _logger;
    private readonly Font _font;

    public RecognitionEndToEndTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _logger = new TestLogger<RecognitionEndToEndTests>(outputHelper);

        // Load font for text rendering
        FontFamily fontFamily;
        if (!SystemFonts.TryGet("Arial", out fontFamily))
        {
            fontFamily = SystemFonts.Collection.Families.First();
        }
        _font = fontFamily.CreateFont(48, FontStyle.Regular);
    }

    [Fact]
    public void CompleteTextRecognitionPipeline_RecognizesRenderedText()
    {
        // Arrange: Create test image with single word
        const string testWord = "HELLO";
        using var testImage = new Image<Rgb24>(200, 48, new Rgb24(255, 255, 255)); // White background, 48px height

        // Draw text in center
        var textSize = TextMeasurer.MeasureSize(testWord, new TextOptions(_font));
        var centerX = (testImage.Width - textSize.Width) / 2;
        var centerY = (testImage.Height - textSize.Height) / 2;
        testImage.Mutate(ctx => ctx.DrawText(testWord, _font, new Rgb24(0, 0, 0), new PointF(centerX, centerY)));

        // Act: Run complete pipeline
        using var session = ModelZoo.GetInferenceSession(Model.SVTRv2);

        // Step 1: Preprocessing
        using var preprocessedBuffer = SVTRv2.PreProcess([testImage]);

        // Step 2: Inference
        using var modelOutput = ModelRunner.Run(session, preprocessedBuffer.AsTensor());

        // Step 3: Post-processing
        var recognizedTexts = SVTRv2.PostProcess(modelOutput);

        // Assert: Verify we recognized the expected text
        Assert.NotEmpty(recognizedTexts);
        var recognizedText = recognizedTexts[0];

        _logger.LogInformation($"Expected: '{testWord}', Recognized: '{recognizedText}'");

        // Allow case-insensitive comparison since font rendering might affect recognition
        Assert.Equal(testWord.ToUpperInvariant(), recognizedText.ToUpperInvariant());

        _logger.LogInformation($"✓ End-to-end text recognition pipeline completed successfully");
    }
}
