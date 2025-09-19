// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Threading.Channels;
using Experimental.Inference;
using Microsoft.ML.OnnxRuntime;
using Ocr;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental;

public class TextReader
{
    private readonly SemaphoreSlim _semaphore;

    private readonly Func<(TextDetector, TextRecognizer)> _factory;

    internal TextReader(Func<(TextDetector, TextRecognizer)> factory, int maxParallelism, int maxBatchSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxParallelism);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBatchSize);

        _factory = factory;
        var capacity = maxParallelism * maxBatchSize * 2;
        _semaphore = new SemaphoreSlim(capacity, capacity);
    }

    public TextReader(ModelRunner dbnetRunner, ModelRunner svtrRunner, int maxParallelism, int maxBatchSize) : this (
        () => (new TextDetector(dbnetRunner), new TextRecognizer(svtrRunner)),
        maxParallelism,
        maxBatchSize)
    { }

    public async IAsyncEnumerable<(List<(TextBoundary BBox, string Text, double Confidence)>, VizBuilder)> ReadMany(IAsyncEnumerable<Image<Rgb24>> images)
    {
        var processingTasks = Channel.CreateUnbounded<Task<(List<(TextBoundary BBox, string Text, double Confidence)>, VizBuilder)>>();

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
    public async Task<Task<(List<(TextBoundary BBox, string Text, double Confidence)>, VizBuilder)>> ReadOne(Image<Rgb24> image)
    {
        await _semaphore.WaitAsync();
        return Task.Run(async () =>
        {
            try
            {
                var (detector, recognizer) = _factory();
                var vizBuilder = new VizBuilder();

                var detections = await detector.Detect(image, vizBuilder);
                var recognitionTasks = detections.Select(d => recognizer.Recognize(d.ORectangle, image, vizBuilder)).ToList();
                var recognitions = await Task.WhenAll(recognitionTasks);
                Debug.Assert(detections.Count == recognitions.Length);
                return (Enumerable.Range(0, detections.Count)
                    .Select(i => (detections[i], recognitions[i].Text, recognitions[i].Confidence))
                    .Where(item => item.Confidence > 0.5)
                    .ToList(), vizBuilder);
            }
            finally
            {
                _semaphore.Release();
            }
        });
    }
}
