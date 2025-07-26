using System.Diagnostics.Metrics;
using Core;
using Ocr.Blocks;
using Ocr.Visualization;
using Resources;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ocr.Test;

/// <summary>
/// Tests for cache-first inference functionality using SVTRBlock
/// </summary>
public class InferenceBlockCacheTests : IDisposable
{
    private readonly Meter _meter;
    private readonly Font _testFont;

    public InferenceBlockCacheTests()
    {
        _meter = new Meter("InferenceBlockCacheTests");
        _testFont = Fonts.GetFont(fontSize: 18f);
    }


    [Fact]
    public async Task CacheFirstInference_TwoDifferentInputs_ReturnsIdenticalWords()
    {
        // Arrange
        using var modelProvider = new ModelProvider();
        var svtrSession = modelProvider.GetSession(Model.SVTRv2);
        var config = new OcrConfiguration { CacheFirstInference = true };
        var svtrBlock = new SVTRBlock(svtrSession, config, _meter);
        await using var bridge = new DataflowBridge<(List<TextBoundary>, Image<Rgb24>, VizBuilder), (Image<Rgb24>, List<TextBoundary>, List<string>, List<double>, VizBuilder)>(svtrBlock.Target);

        // Create two different test inputs
        var (boundaries1, image1, viz1) = CreateTestInput("hello");
        var (boundaries2, image2, viz2) = CreateTestInput("world");

        // Act - Process two different inputs
        var result1Task = await bridge.ProcessAsync((boundaries1, image1, viz1), CancellationToken.None, CancellationToken.None);
        var result1 = await result1Task;

        var result2Task = await bridge.ProcessAsync((boundaries2, image2, viz2), CancellationToken.None, CancellationToken.None);
        var result2 = await result2Task;

        // Assert - With caching enabled, different inputs should produce identical words
        Assert.Equal(result1.Item3, result2.Item3); // Same recognized text
    }

    [Fact]
    public async Task CacheFirstInference_Disabled_TwoDifferentInputs_ReturnsDifferentWords()
    {
        // Arrange
        using var modelProvider = new ModelProvider();
        var svtrSession = modelProvider.GetSession(Model.SVTRv2);
        var config = new OcrConfiguration { CacheFirstInference = false };
        var svtrBlock = new SVTRBlock(svtrSession, config, _meter);
        await using var bridge = new DataflowBridge<(List<TextBoundary>, Image<Rgb24>, VizBuilder), (Image<Rgb24>, List<TextBoundary>, List<string>, List<double>, VizBuilder)>(svtrBlock.Target);

        // Create two different test inputs
        var (boundaries1, image1, viz1) = CreateTestInput("hello");
        var (boundaries2, image2, viz2) = CreateTestInput("world");

        // Act - Process two different inputs
        var result1Task = await bridge.ProcessAsync((boundaries1, image1, viz1), CancellationToken.None, CancellationToken.None);
        var result1 = await result1Task;

        var result2Task = await bridge.ProcessAsync((boundaries2, image2, viz2), CancellationToken.None, CancellationToken.None);
        var result2 = await result2Task;

        // Assert - With caching disabled, different inputs should produce different words
        Assert.NotEqual(result1.Item3, result2.Item3); // Different recognized text
    }

    /// <summary>
    /// Creates test input with a single word image and text boundary
    /// </summary>
    private (List<TextBoundary>, Image<Rgb24>, VizBuilder) CreateTestInput(string text)
    {
        // Create a test image with white background
        var image = new Image<Rgb24>(160, 48, Color.White);

        // Draw the text on the image
        image.Mutate(ctx => ctx.DrawText(text, _testFont, Color.Black, new PointF(10, 10)));

        // Create a text boundary around the word (ensure it fits within image bounds)
        var boundary = TextBoundary.Create(new List<(int X, int Y)>
        {
            (5, 5),
            (154, 5),
            (154, 42),
            (5, 42)
        });

        var boundaries = new List<TextBoundary> { boundary };
        var vizBuilder = VizBuilder.Create(VizMode.None, image);

        return (boundaries, image, vizBuilder);
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }
}
