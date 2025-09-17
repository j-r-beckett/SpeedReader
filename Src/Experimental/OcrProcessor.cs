// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Threading.Channels;
using Ocr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Experimental;

public class OcrProcessor
{
    private readonly SemaphoreSlim _semaphore;

    private readonly Func<TextDetector> _detectorFactory;
    private readonly Func<TextRecognizer> _recognizerFactory;

    public OcrProcessor(Func<TextDetector> detectorFactory, Func<TextRecognizer> recognizerFactory, int maxParallelism, int maxBatchSize)
    {
        _detectorFactory = detectorFactory;
        _recognizerFactory = recognizerFactory;
        var capacity = maxParallelism * maxBatchSize * 2;
        _semaphore = new SemaphoreSlim(capacity, capacity);
    }

    public async IAsyncEnumerable<OcrResult> ProcessMany(IAsyncEnumerable<Image<Rgb24>> images)
    {
        var processingTasks = Channel.CreateUnbounded<Task<OcrResult>>();

        var processingTaskStarter = Task.Run(async () =>
        {
            await foreach (var image in images)
            {
                await processingTasks.Writer.WriteAsync(await Process(image));
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
    public async Task<Task<OcrResult>> Process(Image<Rgb24> image)
    {
        await _semaphore.WaitAsync();
        return Task.Run(async () =>
        {
            try
            {
                var detector = _detectorFactory();
                var recognizer = _recognizerFactory();

                var detections = await detector.Detect(image);
                var recognitionTasks = detections.Select(d => recognizer.Recognize(d, image)).ToList();
                var recognitions = await Task.WhenAll(recognitionTasks);
                return AssembleResults(detections, recognitions.ToList());
            }
            finally
            {
                _semaphore.Release();
            }
        });
    }

    private OcrResult AssembleResults(List<TextBoundary> detections,
        List<(string Text, double Confidence)> recognitions) => new OcrResult();
}
