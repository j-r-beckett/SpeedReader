using Models;
using Ocr.Blocks;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Threading.Tasks.Dataflow;
using Xunit.Abstractions;

namespace Ocr.Test;

[Collection("ONNX")]
public class SVTRBlockTests_dirty
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly Font _font;

    public SVTRBlockTests_dirty(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;

        // Load font for text rendering
        FontFamily fontFamily;
        if (!SystemFonts.TryGet("Arial", out fontFamily))
        {
            fontFamily = SystemFonts.Collection.Families.First();
        }
        _font = fontFamily.CreateFont(32, FontStyle.Regular);
    }

    [Fact]
    public async Task SVTRBlock_WithTextLeftCenter_RecognizesCorrectly()
    {
        // Arrange: Create large image with text at left-center
        const string testText = "HELLO";
        using var testImage = new Image<Rgb24>(800, 400, new Rgb24(255, 255, 255)); // White background

        // Measure text to position it at left-center
        var textSize = TextMeasurer.MeasureSize(testText, new TextOptions(_font));
        var textX = 100; // Left side with some margin
        var textY = (testImage.Height - textSize.Height) / 2; // Vertically centered

        // Draw text at left-center
        testImage.Mutate(ctx => ctx.DrawText(testText, _font, new Rgb24(0, 0, 0), new PointF(textX, textY)));

        // Create bounding box around text (not exact, but reasonable)
        var boundingBox = new Rectangle(
            x: textX - 20,
            y: (int)(textY - 10),
            width: (int)(textSize.Width + 40),
            height: (int)(textSize.Height + 20)
        );

        // Create SVTRBlock
        using var session = ModelZoo.GetInferenceSession(Model.SVTRv2);
        var svtrBlock = SVTRBlock.Create(session);

        // Act: Send input through pipeline
        var input = (testImage, new List<Rectangle> { boundingBox });
        var sent = await svtrBlock.SendAsync(input);
        Assert.True(sent, "Block should accept input");

        svtrBlock.Complete();

        // Wait for result
        var result = await svtrBlock.ReceiveAsync();
        await svtrBlock.Completion;

        // Assert: Verify recognition result
        Assert.NotNull(result);
        Assert.Single(result); // Should have one recognized text for one bounding box

        var recognizedText = result[0];
        _outputHelper.WriteLine($"Expected: '{testText}', Recognized: '{recognizedText}'");

        // Allow case-insensitive comparison and some flexibility in recognition
        Assert.Contains(testText.ToUpperInvariant(), recognizedText.ToUpperInvariant());
    }
}
