using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks;

public static class DBNetBlock
{
    public static IPropagatorBlock<Image<Rgb24>, List<Rectangle>> Create(InferenceSession session)
    {
        var batchBlock = CreateAdaptiveBatchBlock<Image<Rgb24>>();
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

    private static TransformBlock<Image<Rgb24>[], (Buffer<float>, Image<Rgb24>[])> CreatePreProcessingBlock()
    {
        return new TransformBlock<Image<Rgb24>[], (Buffer<float>, Image<Rgb24>[])>(batch => (DBNet.PreProcess(batch), batch));
    }

    private static TransformBlock<(Buffer<float>, Image<Rgb24>[]), (Buffer<float>, Image<Rgb24>[])> CreateModelRunnerBlock(InferenceSession session)
    {
        return new TransformBlock<(Buffer<float> Buffer, Image<Rgb24>[] Batch), (Buffer<float>, Image<Rgb24>[])>(data =>
        {
            var result = ModelRunner.Run(session, data.Buffer.AsTensor());
            data.Buffer.Dispose();
            return (result, data.Batch);
        });
    }

    private static TransformManyBlock<(Buffer<float>, Image<Rgb24>[]), List<Rectangle>> CreatePostProcessingBlock()
    {
        return new TransformManyBlock<(Buffer<float> Buffer, Image<Rgb24>[] Batch), List<Rectangle>>(data =>
        {
            var result = DBNet.PostProcess(data.Buffer, data.Batch);
            data.Buffer.Dispose();
            return result;
        });
    }
}
