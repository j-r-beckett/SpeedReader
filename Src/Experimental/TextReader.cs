// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Threading.Channels;
using Ocr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental;

public class TextReader
{
    private readonly SemaphoreSlim _semaphore;

    private readonly Func<TextDetector> _detectorFactory;
    private readonly Func<TextRecognizer> _recognizerFactory;

    public TextReader(Func<TextDetector> detectorFactory, Func<TextRecognizer> recognizerFactory, int maxParallelism, int maxBatchSize)
    {
        _detectorFactory = detectorFactory;
        _recognizerFactory = recognizerFactory;
        var capacity = maxParallelism * maxBatchSize * 2;
        _semaphore = new SemaphoreSlim(capacity, capacity);
    }

    public async IAsyncEnumerable<List<(TextBoundary BBox, string Text, double Confidence)>> ReadMany(IAsyncEnumerable<Image<Rgb24>> images)
    {
        var processingTasks = Channel.CreateUnbounded<Task<List<(TextBoundary BBox, string Text, double Confidence)>>>();

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
    public async Task<Task<List<(TextBoundary BBox, string Text, double Confidence)>>> ReadOne(Image<Rgb24> image)
    {
        await _semaphore.WaitAsync();
        return Task.Run(async () =>
        {
            try
            {
                var detector = _detectorFactory();
                var recognizer = _recognizerFactory();

                var detections = await detector.Detect(image);
                var recognitionTasks = detections.Select(d => recognizer.Recognize(d.ORectangle, image)).ToList();
                var recognitions = await Task.WhenAll(recognitionTasks);
                Debug.Assert(detections.Count == recognitions.Length);
                return Enumerable.Range(0, detections.Count)
                    .Select(i => (detections[i], recognitions[i].Text, recognitions[i].Confidence))
                    .Where(item => item.Confidence > 0.5)
                    .ToList();
            }
            finally
            {
                _semaphore.Release();
            }
        });
    }
}
