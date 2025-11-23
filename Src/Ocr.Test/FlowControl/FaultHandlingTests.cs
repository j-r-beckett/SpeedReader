// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Ocr.Geometry;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Test.FlowControl;

public class OcrPipelineTests
{
    [Theory]
    [InlineData("detection")]
    [InlineData("recognition")]
    public async Task OcrPipeline_ReadOne_PropagatesException(string stage)
    {
        TextDetector detector;
        TextRecognizer recognizer;
        if (stage == "detection")
        {
            detector = new MockTextDetector((Func<List<BoundingBox>>)(() => throw new TestException()));
            recognizer = new MockTextRecognizer();
        }
        else
        {
            detector = new MockTextDetector();
            recognizer = new MockTextRecognizer((Func<List<(string, double)>>)(() => throw new TestException()));
        }
        var reader = new OcrPipeline(detector, recognizer);

        await Assert.ThrowsAsync<TestException>(async () => await await reader.ReadOne(new Image<Rgb24>(720, 720)));
    }

    [Theory(Skip = "Working on it")]
    [InlineData("detection")]
    [InlineData("recognition")]
    public async Task OcrPipeline_ReadMany_PropagatesExceptionAndContinues(string stage)
    {
        var count = -1;

        TextDetector detector;
        TextRecognizer recognizer;
        if (stage == "detection")
        {
            detector = new MockTextDetector((Func<List<BoundingBox>>)(() =>
            {
                var c = Interlocked.Increment(ref count);
                return c == 1 ? throw new TestException() : MockTextDetector.SimpleResult;
            }));
            recognizer = new MockTextRecognizer();
        }
        else
        {
            detector = new MockTextDetector();
            recognizer = new MockTextRecognizer((Func<List<(string, double)>>)(() =>
            {
                var c = Interlocked.Increment(ref count);
                return c == 1 ? throw new TestException() : MockTextRecognizer.SimpleResult;
            }));
        }
        var reader = new OcrPipeline(detector, recognizer);

        var enumerator = reader.ReadMany(GetImages()).GetAsyncEnumerator();

        // First result should succeed
        var first = await Next(enumerator);
        Assert.NotNull(first);

        // Second result should fail with TestException
        await Assert.ThrowsAsync<TestException>(async () => await Next(enumerator));

        // Third result should succeed
        var third = await Next(enumerator);
        Assert.NotNull(third);

        return;

        static async IAsyncEnumerable<Image<Rgb24>> GetImages()
        {
            for (int i = 0; i < 10; i++)
                yield return new Image<Rgb24>(100, 100);
        }
    }

    private static async Task<T?> Next<T>(IAsyncEnumerator<T> enumerator)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100000));
        await enumerator.MoveNextAsync().AsTask().WaitAsync(cts.Token);
        return enumerator.Current;  // If above returns false, this will return null
    }

    [Fact]
    public async Task ReadMany_PropagatesImageLoadException()
    {
        var detector = new MockTextDetector();
        var recognizer = new MockTextRecognizer();

        var reader = new OcrPipeline(detector, recognizer);

        var results = reader.ReadMany(GetImages());

        // First result should succeed
        var enumerator = results.GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        var firstResult = enumerator.Current;
        Assert.NotNull(firstResult);
        firstResult.Image.Dispose();

        // Second result should fail with TestException
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAsync<TestException>(async () => await Next(enumerator));

        await enumerator.DisposeAsync();

        return;

        static async IAsyncEnumerable<Image<Rgb24>> GetImages()
        {
            yield return new Image<Rgb24>(100, 100); // Valid image

            // Simulate an image loading failure
            await Task.CompletedTask;
            throw new TestException();
        }
    }
}

public class TestException : Exception
{
    public TestException() : base("") { }
}
