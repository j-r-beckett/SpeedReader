// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Test.FlowControl;

public class TextReaderBackpressureTests
{
    [Theory, CombinatorialData]
    public async Task TextReader_ReadOne_EmitsBackpressure(
        [CombinatorialRange(from: 1, count: 3)] int inferenceCapacity)
    {
        var capacity = (int)Math.Ceiling((inferenceCapacity + inferenceCapacity) * 1.5);

        var tcs = new TaskCompletionSource();

        List<Task<Task<OcrPipelineResult>>> results = [];

        var reader = new OcrPipeline(new MockTextDetector(tcs.Task, inferenceCapacity), new MockTextRecognizer(Task.CompletedTask, inferenceCapacity));

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

        await Task.Delay(50);

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

    [Theory, CombinatorialData]
    public async Task TextReader_ReadMany_EmitsBackpressure(
        [CombinatorialRange(from: 1, count: 3)] int inferenceCapacity)
    {
        var capacity = (int)Math.Ceiling((inferenceCapacity + inferenceCapacity) * 1.5);

        var tcs = new TaskCompletionSource();

        var count = 0;

        var incrementAndBlock = async () =>
        {
            Interlocked.Increment(ref count);
            await tcs.Task;
            return MockTextDetector.SimpleResult;
        };

        var reader = new OcrPipeline(new MockTextDetector(incrementAndBlock, inferenceCapacity), new MockTextRecognizer(Task.CompletedTask, inferenceCapacity));

        var image = new Image<Rgb24>(720, 640, Color.White);

        var numInputs = 5 * capacity;

        var inputs = Enumerable.Range(0, numInputs).Select(_ => image).ToAsyncEnumerable();

        var task = Task.Run(async () =>
        {
            await foreach (var _ in reader.ReadMany(inputs))
            {
                // Do nothing
            }
        });

        await Task.Delay(100);

        // Should have read capacity items before blocking
        Assert.Equal(capacity, count);

        // Unblock
        tcs.SetResult();

        await Task.Delay(100);

        // Read the rest of the items
        Assert.Equal(numInputs, count);

        // Make sure we didn't throw any exceptions
        await task;
    }
}
