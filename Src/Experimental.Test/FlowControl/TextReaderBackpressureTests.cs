// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Ocr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental.Test.FlowControl;

public class TextReaderBackpressureTests
{
    [Theory, CombinatorialData]
    public async Task TextReaderEmitsBackpressure(
        [CombinatorialRange(from: 1, count: 3)] int maxParallelism,
        [CombinatorialRange(from: 1, count: 3)] int maxBatchSize,
        bool shouldDbnetBlock)
    {
        var capacity = maxParallelism * maxBatchSize * 2;

        var tcs = new TaskCompletionSource();
        bool shouldSvtrBlock = !shouldDbnetBlock;
        var dbnetBlock = shouldDbnetBlock ? tcs.Task : Task.CompletedTask;
        var svtrBlock = shouldSvtrBlock ? tcs.Task : Task.CompletedTask;

        Func<(TextDetector, TextRecognizer)> factory = () =>
        {
            var detector = new MockTextDetector(dbnetBlock);
            var recognizer = new MockTextRecognizer(svtrBlock);
            return (detector, recognizer);
        };

        List<Task<Task<List<(TextBoundary, string, double)>>>> results = [];

        var reader = new TextReader(factory, maxParallelism, maxBatchSize);

        using var image = new Image<Rgb24>(720, 640, Color.White);

        // Initial submissions should be accepted
        for (var i = 0; i < capacity; i++)
        {
            Assert.False(await IsBlocked());
        }

        // Once we reach capacity, submission should block
        Assert.True(await IsBlocked());

        // Process submissions
        tcs.SetResult();

        // Capacity is free again, should no longer block
        Assert.False(await IsBlocked());

        // Make sure all items complete without exceptions
        await Task.WhenAll(results);

        return;

        async Task<bool> IsBlocked()
        {
            var delay = Task.Delay(TimeSpan.FromMilliseconds(50));
            var submissionTask = reader.ReadOne(image);
            var result = await Task.WhenAny(submissionTask, delay);
            results.Add(submissionTask);
            return result == delay;
        }
    }
}

public class MockTextDetector : TextDetector
{
    private readonly Task _block;

    public MockTextDetector(Task block) : base(null!, null!) => _block = block;

    public override async Task<List<TextBoundary>> Detect(Image<Rgb24> image)
    {
        await _block;

        // Can't return empty, if we do TextRecognizer is never called so it never has the opportunity to block
        return [TextBoundary.Create([(100, 100), (200, 100), (200, 200), (100, 200)])];
    }
}

public class MockTextRecognizer : TextRecognizer
{
    private readonly Task _block;

    public MockTextRecognizer(Task block) : base(null!, null!) => _block = block;

    public override async Task<(string Text, double Confidence)> Recognize(List<(double X, double Y)> oRectangle, Image<Rgb24> image)
    {
        await _block;
        return ("", 0);
    }
}
