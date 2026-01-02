// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SpeedReader.Ocr.Geometry;
using SpeedReader.Ocr.InferenceEngine;
using SpeedReader.Ocr.Visualization;
using SpeedReader.Resources.CharDict;
using Point = SpeedReader.Ocr.Geometry.Point;

namespace SpeedReader.Ocr.Test.FlowControl;

public class MockInferenceEngine : IInferenceEngine
{
    private readonly int _capacity;

    public MockInferenceEngine(int capacity = 1) => _capacity = capacity;

    public Task<(float[] OutputData, int[] OutputShape)> Run(float[] inputData, int[] inputShape)
    {
        var size = inputShape.Aggregate(1, (a, b) => a * b);
        return Task.FromResult((new float[size], inputShape));
    }

    public int CurrentMaxCapacity() => _capacity;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class MockTextDetector : TextDetector
{
    public static readonly List<BoundingBox> SimpleResult =
    [
        new BoundingBox
        {
            Polygon = new Polygon(new List<Point> { (100, 100), (200, 100), (200, 200), (100, 200) }),
            RotatedRectangle = new RotatedRectangle { X = 100, Y = 100, Width = 100, Height = 100, Angle = 0 },
            AxisAlignedRectangle = new AxisAlignedRectangle { X = 100, Y = 100, Width = 100, Height = 100 }
        }
    ];

    private readonly Func<Task<List<BoundingBox>>> _detect;

    public MockTextDetector() : this(() => Task.FromResult(SimpleResult)) { }

    public MockTextDetector(Task block, int capacity = 1) : this(async () =>
    {
        await block;
        return SimpleResult;
    }, capacity)
    {
    }

    public MockTextDetector(Func<List<BoundingBox>> detect, int capacity = 1) : this(() => Task.FromResult(detect()), capacity) { }

    public MockTextDetector(Func<Task<List<BoundingBox>>> detect, int capacity = 1) : base(new MockInferenceEngine(capacity), new DetectionOptions()) => _detect = detect;

    public override async Task<List<BoundingBox>> Detect(Image<Rgb24> image, VizBuilder vizBuilder) => await _detect();
}


public class MockTextRecognizer : TextRecognizer
{
    public static readonly List<(string, double)> SimpleResult = [("", 0)];

    private readonly Func<Task<List<(string, double)>>> _recognize;

    public MockTextRecognizer() : this(() => Task.FromResult(SimpleResult)) { }

    public MockTextRecognizer(Task block, int capacity = 1) : this(async () =>
    {
        await block;
        return SimpleResult;
    }, capacity)
    {
    }

    public MockTextRecognizer(Func<List<(string, double)>> recognize, int capacity = 1) : this(() => Task.FromResult(recognize()), capacity) { }

    public MockTextRecognizer(Func<Task<List<(string, double)>>> recognize, int capacity = 1) : base(new MockInferenceEngine(capacity), new EmbeddedCharDict(), new RecognitionOptions()) => _recognize = recognize;

    public override async Task<List<(string Text, double Confidence)>> Recognize(List<BoundingBox> regions, Image<Rgb24> image, VizBuilder vizBuilder) => await _recognize();
}

// MockCpuModelRunner commented out - CpuModelRunner has been replaced by IInferenceEngine
/*
public class MockCpuModelRunner : CpuModelRunner
{
    public static readonly (float[] Data, int[] Shape) SimpleResult = ([0], [1, 1]);

    private readonly Func<(float[], int[])> _infer;

    public MockCpuModelRunner(int maxParallelism = 1) : this(() => SimpleResult) { }

    public MockCpuModelRunner(Func<(float[], int[])> infer, int initialParallelism = 1) : base(null!, initialParallelism) => _infer = infer;

    protected override (float[] Data, int[] Shape) RunInferenceInternal(float[] batch, int[] shape) => _infer();
}
*/
