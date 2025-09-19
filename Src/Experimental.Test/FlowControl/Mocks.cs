// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Ocr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental.Test.FlowControl;

public class MockTextDetector : TextDetector
{
    public static readonly List<TextBoundary> SimpleResult
        = [TextBoundary.Create([(100, 100), (200, 100), (200, 200), (100, 200)])];

    private readonly Func<Task<List<TextBoundary>>> _detect;

    public MockTextDetector() : this(() => Task.FromResult(SimpleResult)) { }

    public MockTextDetector(Task block) : this(async () =>
    {
        await block;
        return SimpleResult;
    })
    {
    }

    public MockTextDetector(Func<List<TextBoundary>> detect) : this(() => Task.FromResult(detect())) { }

    public MockTextDetector(Func<Task<List<TextBoundary>>> detect) : base(null!, null!) => _detect = detect;

    public override async Task<List<TextBoundary>> Detect(Image<Rgb24> image) => await _detect();
}


public class MockTextRecognizer : TextRecognizer
{
    public static readonly (string, double) SimpleResult = ("", 0);

    private readonly Func<Task<(string, double)>> _recognize;

    public MockTextRecognizer() : this(() => Task.FromResult(SimpleResult)) { }

    public MockTextRecognizer(Task block) : this(async () =>
    {
        await block;
        return SimpleResult;
    })
    {
    }

    public MockTextRecognizer(Func<(string, double)> recognize) : this(() => Task.FromResult(recognize())) { }

    public MockTextRecognizer(Func<Task<(string, double)>> recognize) : base(null!, null!) => _recognize = recognize;

    public override async Task<(string Text, double Confidence)> Recognize(List<(double X, double Y)> oRectangle, Image<Rgb24> image) => await _recognize();
}
