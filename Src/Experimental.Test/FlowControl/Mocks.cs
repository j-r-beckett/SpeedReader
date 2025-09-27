// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Immutable;
using Experimental.Geometry;
using Experimental.Inference;
using Experimental.Visualization;
using Ocr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Point = Experimental.Geometry.Point;

namespace Experimental.Test.FlowControl;

public class MockTextDetector : TextDetector
{
    public static readonly List<BoundingBox> SimpleResult
        = [BoundingBox.Create(new Polygon { Points = new List<Point> { (100, 100), (200, 100), (200, 200), (100, 200) }.ToImmutableList() })!];

    private readonly Func<Task<List<BoundingBox>>> _detect;

    public MockTextDetector() : this(() => Task.FromResult(SimpleResult)) { }

    public MockTextDetector(Task block) : this(async () =>
    {
        await block;
        return SimpleResult;
    })
    {
    }

    public MockTextDetector(Func<List<BoundingBox>> detect) : this(() => Task.FromResult(detect())) { }

    public MockTextDetector(Func<Task<List<BoundingBox>>> detect) : base(null!) => _detect = detect;

    public override async Task<List<BoundingBox>> Detect(Image<Rgb24> image, VizBuilder vizBuilder) => await _detect();
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

    public MockTextRecognizer(Func<Task<(string, double)>> recognize) : base(null!) => _recognize = recognize;

    public override async Task<(string Text, double Confidence)> Recognize(RotatedRectangle region, Image<Rgb24> image, VizBuilder vizBuilder) => await _recognize();
}

public class MockCpuModelRunner : CpuModelRunner
{
    public static readonly (float[] Data, int[] Shape) SimpleResult = ([0], [1, 1]);

    private readonly Func<(float[], int[])> _infer;

    public MockCpuModelRunner(int maxParallelism = 1) : this(() => SimpleResult) { }

    public MockCpuModelRunner(Func<(float[], int[])> infer, int maxParallelism = 1) : base(null!, maxParallelism) => _infer = infer;

    protected override (float[] Data, int[] Shape) RunInferenceInternal(float[] batch, int[] shape) => _infer();
}
