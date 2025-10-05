// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Experimental.Geometry;
using Experimental.Inference;
using Ocr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental.Test.FlowControl;

public class FaultHandlingTests
{
    [Fact]
    public async Task TextReader_PropagatesDetectionException()
    {
        Func<(TextDetector, TextRecognizer)> factory = () =>
        {
            var detector = new MockTextDetector((Func<List<BoundingBox>>)(() => throw new TestException()));
            var recognizer = new MockTextRecognizer();
            return (detector, recognizer);
        };

        var reader = new SpeedReader(factory, 1, 1);

        await Assert.ThrowsAsync<TestException>(async () => await await reader.ReadOne(new Image<Rgb24>(720, 720)));
    }

    [Fact]
    public async Task TextReader_PropagatesRecognitionException()
    {
        Func<(TextDetector, TextRecognizer)> factory = () =>
        {
            var detector = new MockTextDetector();
            var recognizer = new MockTextRecognizer((Func<List<(string, double)>>)(() => throw new TestException()));
            return (detector, recognizer);
        };

        var reader = new SpeedReader(factory, 1, 1);

        await Assert.ThrowsAsync<TestException>(async () => await await reader.ReadOne(new Image<Rgb24>(720, 720)));
    }

    [Fact]
    public async Task CpuModelRunner_PropagatesException()
    {
        var counter = -1;

        // Throw an exception on the second call only
        var infer = () =>
        {
            var count = Interlocked.Increment(ref counter);
            return count == 1 ? throw new InferenceException("", new TestException()) : MockCpuModelRunner.SimpleResult;
        };

        var runner = new MockCpuModelRunner(infer);

        // First call should succeed
        var firstResult = await await runner.Run([0], [1, 1]);

        Assert.Equivalent(firstResult.Data, MockCpuModelRunner.SimpleResult.Data);
        Assert.Equivalent(firstResult.Shape, MockCpuModelRunner.SimpleResult.Shape);

        // Second call should fail
        var ex = await Assert.ThrowsAnyAsync<InferenceException>(async () => await await runner.Run([0], [1, 1]));
        Assert.IsType<TestException>(ex.InnerException);

        // Third call should succeed
        var secondResult = await await runner.Run([0], [1, 1]);

        Assert.Equivalent(secondResult.Data, MockCpuModelRunner.SimpleResult.Data);
        Assert.Equivalent(secondResult.Shape, MockCpuModelRunner.SimpleResult.Shape);

        await runner.DisposeAsync();
    }

    [Fact]
    public async Task ReadMany_PropagatesImageLoadException()
    {
        Func<(TextDetector, TextRecognizer)> factory = () =>
        {
            var detector = new MockTextDetector();
            var recognizer = new MockTextRecognizer();
            return (detector, recognizer);
        };

        var reader = new SpeedReader(factory, 1, 1);

        static async IAsyncEnumerable<Image<Rgb24>> GetImages()
        {
            yield return new Image<Rgb24>(100, 100); // Valid image

            // Simulate an image loading failure
            await Task.CompletedTask;
            throw new TestException();
        }

        var results = reader.ReadMany(GetImages());

        // First result should succeed
        var enumerator = results.GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        var firstResult = enumerator.Current;
        Assert.NotNull(firstResult);
        firstResult.Image.Dispose();

        // Second result should fail with TestException
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAsync<TestException>(async () => await enumerator.MoveNextAsync().AsTask().WaitAsync(cts.Token));

        await enumerator.DisposeAsync();
    }
}

public class TestException : Exception
{
    public TestException() : base("") { }
}
