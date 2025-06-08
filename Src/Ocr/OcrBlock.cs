using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ProcessingData = (Ocr.Buffer<float> Buffer, (int, int)[] Dimensions);

namespace Ocr;

public static class OcrBlock
{
    public static IPropagatorBlock<Image<Rgb24>, List<Rectangle>> CreateOcrBlock(InferenceSession session)
    {
        var batchBlock = CreateGenerousBatchBlock<Image<Rgb24>>();
        var preProcessingBlock = CreatePreProcessingBlock();
        var modelRunnerBlock = CreateModelRunnerBlock(session);
        var postProcessingBlock = CreatePostProcessingBlock();

        batchBlock.LinkTo(preProcessingBlock, new DataflowLinkOptions { PropagateCompletion = true });
        preProcessingBlock.LinkTo(modelRunnerBlock, new DataflowLinkOptions { PropagateCompletion = true });
        modelRunnerBlock.LinkTo(postProcessingBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return DataflowBlock.Encapsulate(batchBlock, postProcessingBlock);
    }

    private static IPropagatorBlock<T, T[]> CreateGenerousBatchBlock<T>()
    {
        return new TransformBlock<T, T[]>(data => [data]);
    }

    private static TransformBlock<Image<Rgb24>[], ProcessingData> CreatePreProcessingBlock()
    {
        return new TransformBlock<Image<Rgb24>[], ProcessingData>(DBNet.PreProcess);
    }

    private static TransformBlock<ProcessingData, ProcessingData> CreateModelRunnerBlock(InferenceSession session)
    {
        return new TransformBlock<ProcessingData, ProcessingData>(data =>
        {
            var result = ModelRunner.Run(session, data.Buffer.AsTensor());
            data.Buffer.Dispose();
            return (result, data.Dimensions);
        });
    }

    private static TransformManyBlock<ProcessingData, List<Rectangle>> CreatePostProcessingBlock()
    {
        return new TransformManyBlock<ProcessingData, List<Rectangle>>(data =>
        {
            var result = DBNet.PostProcess(data.Buffer, data.Dimensions);
            data.Buffer.Dispose();
            return result;
        });
    }
}
