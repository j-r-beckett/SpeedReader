// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

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
            var detector = new MockTextDetector((Func<List<TextBoundary>>)(() => throw new TestException()));
            var recognizer = new MockTextRecognizer();
            return (detector, recognizer);
        };

        var reader = new TextReader(factory, 1, 1);

        await Assert.ThrowsAsync<TestException>(async () => await await reader.ReadOne(new Image<Rgb24>(720, 720)));
    }

    [Fact]
    public async Task TextReader_PropagatesRecognitionException()
    {
        Func<(TextDetector, TextRecognizer)> factory = () =>
        {
            var detector = new MockTextDetector();
            var recognizer = new MockTextRecognizer((Func<(string, double)>)(() => throw new TestException()));
            return (detector, recognizer);
        };

        var reader = new TextReader(factory, 1, 1);

        await Assert.ThrowsAsync<TestException>(async () => await await reader.ReadOne(new Image<Rgb24>(720, 720)));
    }
}

public class TestException : Exception
{
    public TestException() : base("") { }
}
