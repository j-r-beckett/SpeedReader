// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics.Metrics;
using System.Threading.Tasks.Dataflow;
using Core;
using Ocr.Blocks;
using Ocr.Visualization;
using Resources;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Video.Test;
using Xunit.Abstractions;

namespace Ocr.Test.E2E;

public class VideoOcrTests
{
    private readonly Font _testFont;
    private readonly ITestOutputHelper _outputHelper;

    public VideoOcrTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _testFont = Fonts.GetFont(fontSize: 48f);
    }

    [Fact]
    public async Task VideoOcrBlock_SingleFrameWithHello_ReturnsCorrectText()
    {
        // Arrange - Create a single frame with "hello" text
        var width = 800;
        var height = 600;
        var frameRate = 1.0; // 1 fps for simplicity

        var image = new Image<Rgb24>(width, height, Color.White);

        // Draw "hello" in the center of the image
        var text = "hello";
        var textOptions = new TextOptions(_testFont);
        var textSize = TextMeasurer.MeasureAdvance(text, textOptions);
        var x = (width - textSize.Width) / 2;
        var y = (height - textSize.Height) / 2;

        image.Mutate(ctx => ctx.DrawText(text, _testFont, Color.Black, new PointF(x, y)));

        // Create video from the single frame
        var frames = CreateSingleFrameAsyncEnumerable(image);
        var videoStream = await FrameWriter.ToCompressedVideo(width, height, frameRate, frames, CancellationToken.None);

        // Act - Process video through VideoOcrBlock
        using var modelProvider = new ModelProvider();
        var dbnetSession = modelProvider.GetSession(Model.DbNet18, ModelPrecision.INT8);
        var svtrSession = modelProvider.GetSession(Model.SVTRv2);
        using var meter = new Meter("VideoOcrTests");

        var ocrBlock = new OcrBlock(dbnetSession, svtrSession, new OcrConfiguration(), meter);
        var videoOcrBlock = new VideoOcrBlock(ocrBlock, videoStream, sampleRate: 1);

        var results = new List<(Image<Rgb24>, OcrResult)>();
        var consumeBlock = new ActionBlock<(Image<Rgb24>, OcrResult, VizBuilder)>(result =>
        {
            results.Add((result.Item1, result.Item2));
        });

        videoOcrBlock.Source.LinkTo(consumeBlock, new DataflowLinkOptions { PropagateCompletion = true });

        await consumeBlock.Completion;

        // Assert
        Assert.NotEmpty(results);
        var firstResult = results.First();

        Assert.NotNull(firstResult.Item2);
        Assert.NotEmpty(firstResult.Item2.Words);

        // Find the word that matches "hello"
        var recognizedWords = firstResult.Item2.Words.Select(w => w.Text.ToLower().Trim()).ToList();
        _outputHelper.WriteLine($"Recognized words: {string.Join(", ", recognizedWords)}");

        Assert.Contains("hello", recognizedWords);

        // Verify the image dimensions match
        Assert.Equal(width, firstResult.Item1.Width);
        Assert.Equal(height, firstResult.Item1.Height);
    }

    [Fact]
    public async Task VideoOcrBlock_ThreeFramesWithMovingHello_TracksTextAcrossFrames()
    {
        // Arrange - Create three frames with "hello" moving from left to right
        var width = 800;
        var height = 600;
        var frameRate = 3.0; // 3 fps

        var frames = new List<Image<Rgb24>>();
        var text = "hello";
        var textOptions = new TextOptions(_testFont);
        var textSize = TextMeasurer.MeasureAdvance(text, textOptions);
        var y = (height - textSize.Height) / 2; // Keep Y position constant (centered vertically)

        // Frame 1: hello on the left
        var frame1 = new Image<Rgb24>(width, height, Color.White);
        var x1 = 50; // Near left edge
        frame1.Mutate(ctx => ctx.DrawText(text, _testFont, Color.Black, new PointF(x1, y)));
        frames.Add(frame1);

        // Frame 2: hello in the center
        var frame2 = new Image<Rgb24>(width, height, Color.White);
        var x2 = (width - textSize.Width) / 2; // Center
        frame2.Mutate(ctx => ctx.DrawText(text, _testFont, Color.Black, new PointF(x2, y)));
        frames.Add(frame2);

        // Frame 3: hello on the right
        var frame3 = new Image<Rgb24>(width, height, Color.White);
        var x3 = width - textSize.Width - 50; // Near right edge
        frame3.Mutate(ctx => ctx.DrawText(text, _testFont, Color.Black, new PointF(x3, y)));
        frames.Add(frame3);

        // Create video from the three frames
        var framesEnumerable = CreateMultipleFramesAsyncEnumerable(frames);
        var videoStream = await FrameWriter.ToCompressedVideo(width, height, frameRate, framesEnumerable, CancellationToken.None);

        // Act - Process video through VideoOcrBlock
        using var modelProvider = new ModelProvider();
        var dbnetSession = modelProvider.GetSession(Model.DbNet18, ModelPrecision.INT8);
        var svtrSession = modelProvider.GetSession(Model.SVTRv2);
        using var meter = new Meter("VideoOcrTests");

        var ocrBlock = new OcrBlock(dbnetSession, svtrSession, new OcrConfiguration(), meter);
        var videoOcrBlock = new VideoOcrBlock(ocrBlock, videoStream, sampleRate: 1);

        var results = new List<(Image<Rgb24>, OcrResult)>();
        var consumeBlock = new ActionBlock<(Image<Rgb24>, OcrResult, VizBuilder)>(result =>
        {
            results.Add((result.Item1, result.Item2));
        });

        videoOcrBlock.Source.LinkTo(consumeBlock, new DataflowLinkOptions { PropagateCompletion = true });

        await consumeBlock.Completion;

        // Assert
        Assert.Equal(3, results.Count); // Should have exactly 3 frames

        // Verify each frame detected "hello"
        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            Assert.NotNull(result.Item2);
            Assert.NotEmpty(result.Item2.Words);

            var recognizedWords = result.Item2.Words.Select(w => w.Text.ToLower().Trim()).ToList();
            Assert.Contains("hello", recognizedWords);

            // Log the position for debugging
            var word = result.Item2.Words.First();
            var boundingBox = word.BoundingBox.AARectangle;
            _outputHelper.WriteLine($"Frame {i + 1}: Text '{word.Text}' at X={boundingBox.X:F3}, ID={word.Id}");
        }

        // Verify that the text positions are different (moving left to right)
        var positions = results.Select(r => r.Item2.Words.First().BoundingBox.AARectangle.X).ToList();
        Assert.True(positions[0] < positions[1], "Text should move from left to center");
        Assert.True(positions[1] < positions[2], "Text should move from center to right");

        // Verify deduplication - all three "hello" instances should have the same ID
        var wordIds = results.Select(r => r.Item2.Words.First().Id).ToList();
        Assert.Equal(wordIds[0], wordIds[1]); // Frame 1 and 2 should have same ID
        Assert.Equal(wordIds[1], wordIds[2]); // Frame 2 and 3 should have same ID
        _outputHelper.WriteLine($"All 'hello' instances have ID: {wordIds[0]}");
    }

    private static async IAsyncEnumerable<Image<Rgb24>> CreateSingleFrameAsyncEnumerable(Image<Rgb24> frame)
    {
        yield return frame;
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<Image<Rgb24>> CreateMultipleFramesAsyncEnumerable(List<Image<Rgb24>> frames)
    {
        foreach (var frame in frames)
        {
            yield return frame;
            await Task.Delay(1); // Small delay to simulate async behavior
        }
    }
}
