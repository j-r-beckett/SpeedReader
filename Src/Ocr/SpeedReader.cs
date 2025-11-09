// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Threading.Channels;
using Ocr.Controls;
using Ocr.InferenceEngine.Engines;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr;

public class SpeedReader
{
    private readonly TextDetector _detector;
    private readonly TextRecognizer _recognizer;
    private readonly Executor<Task<Image<Rgb24>>, SpeedReaderResult> _executor;

    public SpeedReader(TextDetector detector, TextRecognizer recognizer, int maxParallelism, int maxBatchSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxParallelism);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBatchSize);

        _detector = detector;
        _recognizer = recognizer;
        var capacity = maxParallelism * maxBatchSize * 2;
        _executor = new Executor<Task<Image<Rgb24>>, SpeedReaderResult>(Execute, capacity);
    }

    // Legacy constructor for backward compatibility with IInferenceEngine
    public SpeedReader(IInferenceEngine dbnetEngine, IInferenceEngine svtrEngine, int maxParallelism, int maxBatchSize)
        : this(new TextDetector(dbnetEngine), new TextRecognizer(svtrEngine), maxParallelism, maxBatchSize)
    {
    }

    // Legacy constructor for backward compatibility with factory pattern
    internal SpeedReader(Func<(TextDetector, TextRecognizer)> factory, int maxParallelism, int maxBatchSize)
        : this(factory().Item1, factory().Item2, maxParallelism, maxBatchSize)
    {
    }

    public IAsyncEnumerable<SpeedReaderResult> ReadMany(IAsyncEnumerable<string> paths) =>
        ReadMany(paths.Select(path => Image.LoadAsync<Rgb24>(path)));

    public Task<Task<SpeedReaderResult>> ReadOne(string path) => ReadOne(Image.LoadAsync<Rgb24>(path));

    public IAsyncEnumerable<SpeedReaderResult> ReadMany(IAsyncEnumerable<Image<Rgb24>> images) =>
        ReadMany(images.Select(Task.FromResult));

    public Task<Task<SpeedReaderResult>> ReadOne(Image<Rgb24> image) => ReadOne(Task.FromResult(image));

    private async IAsyncEnumerable<SpeedReaderResult> ReadMany(IAsyncEnumerable<Task<Image<Rgb24>>> images)
    {
        var processingTasks = Channel.CreateUnbounded<Task<SpeedReaderResult>>();

        var processingTaskStarter = Task.Run(async () =>
        {
            try
            {
                await foreach (var image in images)
                {
                    await processingTasks.Writer.WriteAsync(await ReadOne(image));
                }
            }
            finally
            {
                processingTasks.Writer.Complete();
            }
        });

        await foreach (var task in processingTasks.Reader.ReadAllAsync())
        {
            yield return await task;
        }

        await processingTaskStarter;
    }

    private Task<Task<SpeedReaderResult>> ReadOne(Task<Image<Rgb24>> imageTask) => _executor.ExecuteSingle(imageTask);

    private async Task<SpeedReaderResult> Execute(Task<Image<Rgb24>> imageTask)
    {
        var vizBuilder = new VizBuilder();

        var image = await imageTask;
        var detections = await _detector.Detect(image, vizBuilder);
        var recognitions = await _recognizer.Recognize(detections, image, vizBuilder);
        Debug.Assert(detections.Count == recognitions.Count);
        return new SpeedReaderResult(image, detections, recognitions.ToList(), vizBuilder);
    }
}
