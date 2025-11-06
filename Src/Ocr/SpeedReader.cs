// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Threading.Channels;
using Ocr.Inference;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr;

public class SpeedReader
{
    private readonly SemaphoreSlim _semaphore;

    private readonly Func<(TextDetector, TextRecognizer)> _factory;

    internal SpeedReader(Func<(TextDetector, TextRecognizer)> factory, int maxParallelism, int maxBatchSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxParallelism);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBatchSize);

        _factory = factory;
        var capacity = maxParallelism * maxBatchSize * 2;
        _semaphore = new SemaphoreSlim(capacity, capacity);
    }

    public SpeedReader(ModelRunner dbnetRunner, ModelRunner svtrRunner, int capacity)
    {
        _factory = () => (new TextDetector(dbnetRunner), new TextRecognizer(svtrRunner));
        _semaphore = new SemaphoreSlim(capacity, capacity);
    }

    public SpeedReader(ModelRunner dbnetRunner, ModelRunner svtrRunner, int maxParallelism, int maxBatchSize) : this(
        () => (new TextDetector(dbnetRunner), new TextRecognizer(svtrRunner)),
        maxParallelism,
        maxBatchSize)
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

    private async Task<Task<SpeedReaderResult>> ReadOne(Task<Image<Rgb24>> imageTask)
    {
        await _semaphore.WaitAsync();
        return Task.Run(async () =>
        {
            try
            {
                var (detector, recognizer) = _factory();
                var vizBuilder = new VizBuilder();

                var image = await imageTask;
                var detections = await detector.Detect(image, vizBuilder);
                var recognitions = await recognizer.Recognize(detections, image, vizBuilder);
                Debug.Assert(detections.Count == recognitions.Count);
                return new SpeedReaderResult(image, detections, recognitions.ToList(), vizBuilder);
            }
            finally
            {
                _semaphore.Release();
            }
        });
    }
}
