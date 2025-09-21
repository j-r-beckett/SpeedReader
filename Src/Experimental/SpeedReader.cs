// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Threading.Channels;
using Experimental.BoundingBoxes;
using Experimental.Inference;
using Ocr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental;

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

    public SpeedReader(ModelRunner dbnetRunner, ModelRunner svtrRunner, int maxParallelism, int maxBatchSize) : this(
        () => (new TextDetector(dbnetRunner), new TextRecognizer(svtrRunner)),
        maxParallelism,
        maxBatchSize)
    {
    }

    public async IAsyncEnumerable<SpeedReaderResult> ReadMany(IAsyncEnumerable<Image<Rgb24>> images)
    {
        var processingTasks = Channel.CreateUnbounded<Task<SpeedReaderResult>>();

        var processingTaskStarter = Task.Run(async () =>
        {
            await foreach (var image in images)
            {
                await processingTasks.Writer.WriteAsync(await ReadOne(image));
            }

            processingTasks.Writer.Complete();
        });

        await foreach (var task in processingTasks.Reader.ReadAllAsync())
        {
            yield return await task;
        }

        await processingTaskStarter;
    }

    // Outer task (first await) is the handoff, inner task (second await) is actual processing
    public async Task<Task<SpeedReaderResult>> ReadOne(Image<Rgb24> image)
    {
        await _semaphore.WaitAsync();
        return Task.Run(async () =>
        {
            try
            {
                var (detector, recognizer) = _factory();
                var vizBuilder = new VizBuilder();

                var detections = await detector.Detect(image, vizBuilder);
                var recognitionTasks = detections.Select(d =>
                    recognizer.Recognize(d.RotatedRectangle, image, vizBuilder));
                var recognitions = await Task.WhenAll(recognitionTasks);
                Debug.Assert(detections.Count == recognitions.Length);
                return new SpeedReaderResult(image, detections, recognitions.ToList(), vizBuilder);
            }
            finally
            {
                _semaphore.Release();
            }
        });
    }
}
