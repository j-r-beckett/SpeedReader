using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks;

public static class SVTRBlock
{
    public static IPropagatorBlock<(Image<Rgb24>, List<Rectangle>), (Image<Rgb24>, List<Rectangle>, List<string>)> Create(InferenceSession session)
    {
        var batchBlock = CreateAdaptiveBatchBlock<(Image<Rgb24>, List<Rectangle>)>();
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

    private static TransformBlock<(Image<Rgb24>, List<Rectangle>)[], (Buffer<float>, (Image<Rgb24>, List<Rectangle>)[])> CreatePreProcessingBlock()
    {
        return new TransformBlock<(Image<Rgb24>, List<Rectangle>)[], (Buffer<float>, (Image<Rgb24>, List<Rectangle>)[])>(batch =>
        {
            var images = batch.Select(item => item.Item1).ToArray();
            var rectangles = batch.Select(item => item.Item2).ToArray();

            var buffer = SVTRv2.PreProcess(images, rectangles);
            return (buffer, batch);
        });
    }

    private static TransformBlock<(Buffer<float>, (Image<Rgb24>, List<Rectangle>)[]), (Buffer<float>, (Image<Rgb24>, List<Rectangle>)[])> CreateModelRunnerBlock(InferenceSession session)
    {
        return new TransformBlock<(Buffer<float> Buffer, (Image<Rgb24>, List<Rectangle>)[] Batch), (Buffer<float>, (Image<Rgb24>, List<Rectangle>)[])>(data =>
        {
            var result = ModelRunner.Run(session, data.Buffer.AsTensor());
            data.Buffer.Dispose();
            return (result, data.Batch);
        });
    }

    private static TransformManyBlock<(Buffer<float>, (Image<Rgb24>, List<Rectangle>)[]), (Image<Rgb24>, List<Rectangle>, List<string>)> CreatePostProcessingBlock()
    {
        return new TransformManyBlock<(Buffer<float> Buffer, (Image<Rgb24>, List<Rectangle>)[] Batch), (Image<Rgb24>, List<Rectangle>, List<string>)>(data =>
        {
            var recognizedTexts = SVTRv2.PostProcess(data.Buffer);
            data.Buffer.Dispose();

            // Return tuple combining original images, rectangles, and recognized texts
            var results = new List<(Image<Rgb24>, List<Rectangle>, List<string>)>();
            int textIndex = 0;

            foreach (var (image, rectangles) in data.Batch)
            {
                var batchTexts = new List<string>();
                for (int i = 0; i < rectangles.Count; i++)
                {
                    batchTexts.Add(recognizedTexts[textIndex++]);
                }
                results.Add((image, rectangles, batchTexts));
            }

            return results;
        });
    }
}
