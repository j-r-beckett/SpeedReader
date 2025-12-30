// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Threading.Channels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SpeedReader.Ocr.Visualization;

namespace SpeedReader.Ocr;

public class OcrPipeline
{
    private readonly TextDetector _detector;
    private readonly TextRecognizer _recognizer;
    private readonly TaskPool<OcrPipelineResult> _taskPool;

    public OcrPipeline(TextDetector detector, TextRecognizer recognizer)
    {
        _detector = detector;
        _recognizer = recognizer;
        _taskPool = new TaskPool<OcrPipelineResult>();
        RightsizePool();
    }

    private void RightsizePool()
    {
        var targetSize = (_detector.InferenceEngineCapacity() + _recognizer.InferenceEngineCapacity()) * 1.5;
        _taskPool.SetPoolSize((int)Math.Ceiling(targetSize));
    }

    public IAsyncEnumerable<Result<OcrPipelineResult>> ReadMany(IAsyncEnumerable<string> paths) =>
        ReadMany(paths.Select(path => Image.LoadAsync<Rgb24>(path)));

    public Task<Task<OcrPipelineResult>> ReadOne(string path) => ReadOne(Image.LoadAsync<Rgb24>(path));

    public IAsyncEnumerable<Result<OcrPipelineResult>> ReadMany(IAsyncEnumerable<Image<Rgb24>> images) =>
        ReadMany(images.Select(Task.FromResult));

    public Task<Task<OcrPipelineResult>> ReadOne(Image<Rgb24> image) => ReadOne(Task.FromResult(image));

    private async IAsyncEnumerable<Result<OcrPipelineResult>> ReadMany(IAsyncEnumerable<Task<Image<Rgb24>>> images)
    {
        var processingTasks = Channel.CreateUnbounded<Task<OcrPipelineResult>>();

        var processingTaskStarter = Task.Run(async () =>
        {
            try
            {
                await foreach (var image in images)
                {
                    await processingTasks.Writer.WriteAsync(await ReadOne(image));
                }
            }
            catch (Exception ex)
            {
                await processingTasks.Writer.WriteAsync(Task.FromException<OcrPipelineResult>(ex));
            }
            finally
            {
                processingTasks.Writer.Complete();
            }
        });

        await foreach (var task in processingTasks.Reader.ReadAllAsync())
        {
            Result<OcrPipelineResult> result;
            try
            {
                result = new Result<OcrPipelineResult>(await task);
            }
            catch (Exception ex)
            {
                result = new Result<OcrPipelineResult>(ex);
            }

            yield return result;
        }

        await processingTaskStarter;
    }

    private Task<Task<OcrPipelineResult>> ReadOne(Task<Image<Rgb24>> imageTask)
    {
        return _taskPool.Execute(Execute);

        async Task<OcrPipelineResult> Execute()
        {
            var vizBuilder = new VizBuilder();
            var image = await imageTask;
            var detections = await _detector.Detect(image, vizBuilder);
            var recognitions = await _recognizer.Recognize(detections, image, vizBuilder);
            Debug.Assert(detections.Count == recognitions.Count);
            return new OcrPipelineResult(image, detections, recognitions.ToList(), vizBuilder);
        }
    }
}
