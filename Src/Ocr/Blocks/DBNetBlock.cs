using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks;

public static class DBNetBlock
{
    public static IPropagatorBlock<(Image<Rgb24>, VizBuilder), (Image<Rgb24>, List<Rectangle>, VizBuilder)> Create(InferenceSession session)
    {
        var batchBlock = CreateAdaptiveBatchBlock<(Image<Rgb24>, VizBuilder)>();
        var preProcessingBlock = CreatePreProcessingBlock();
        var modelRunnerBlock = CreateModelRunnerBlock(session);
        var postProcessingBlock = CreatePostProcessingBlock();

        batchBlock.LinkTo(preProcessingBlock, new DataflowLinkOptions { PropagateCompletion = true });
        preProcessingBlock.LinkTo(modelRunnerBlock, new DataflowLinkOptions { PropagateCompletion = true });
        modelRunnerBlock.LinkTo(postProcessingBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return DataflowBlock.Encapsulate(batchBlock, postProcessingBlock);
    }

    private static TransformBlock<T, T[]> CreateAdaptiveBatchBlock<T>()
    {
        return new TransformBlock<T, T[]>(data => [data]);
    }

    private static TransformBlock<(Image<Rgb24>, VizBuilder)[], (Buffer<float>, (Image<Rgb24>, VizBuilder)[])> CreatePreProcessingBlock()
    {
        return new TransformBlock<(Image<Rgb24>, VizBuilder)[], (Buffer<float>, (Image<Rgb24>, VizBuilder)[])>(batch =>
        {
            var images = batch.Select(b => b.Item1).ToArray();
            var buffer = DBNet.PreProcess(images);
            return (buffer, batch);
        });
    }

    private static TransformBlock<(Buffer<float>, (Image<Rgb24>, VizBuilder)[]), (Buffer<float>, (Image<Rgb24>, VizBuilder)[])> CreateModelRunnerBlock(InferenceSession session)
    {
        return new TransformBlock<(Buffer<float> Buffer, (Image<Rgb24>, VizBuilder)[] Batch), (Buffer<float>, (Image<Rgb24>, VizBuilder)[])>(data =>
        {
            var result = ModelRunner.Run(session, data.Buffer.AsTensor());
            data.Buffer.Dispose();
            return (result, data.Batch);
        });
    }

    private static TransformManyBlock<(Buffer<float>, (Image<Rgb24>, VizBuilder)[]), (Image<Rgb24>, List<Rectangle>, VizBuilder)> CreatePostProcessingBlock()
    {
        return new TransformManyBlock<(Buffer<float> Buffer, (Image<Rgb24>, VizBuilder)[] Batch), (Image<Rgb24>, List<Rectangle>, VizBuilder)>(data =>
        {
            var images = data.Batch.Select(b => b.Item1).ToArray();
            var rectangleResults = DBNet.PostProcess(data.Buffer, images);
            data.Buffer.Dispose();

            // Return tuple combining original images with their detected rectangles and VizBuilder
            var results = new List<(Image<Rgb24>, List<Rectangle>, VizBuilder)>();
            for (int i = 0; i < data.Batch.Length; i++)
            {
                results.Add((data.Batch[i].Item1, rectangleResults[i], data.Batch[i].Item2));
            }
            return results;
        });
    }
}
