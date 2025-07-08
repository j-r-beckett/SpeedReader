using System.Threading.Tasks.Dataflow;
using CommunityToolkit.HighPerformance;
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
            // Get dimensions from buffer
            int height = (int)data.Buffer.Shape[1];
            int width = (int)data.Buffer.Shape[2];
            int imageSize = height * width;

            // Binarize for connected component analysis
            // Algorithms.Thresholding.BinarizeInPlace(data.Buffer.AsTensor(), 0.2f);

            var results = new List<(Image<Rgb24>, List<Rectangle>, VizBuilder)>();

            for (int i = 0; i < data.Batch.Length; i++)
            {
                var (originalImage, vizBuilder) = data.Batch[i];

                var probabilityMapSlice = data.Buffer.AsSpan().Slice(i * imageSize, imageSize).AsSpan2D(height, width);

                vizBuilder.AddProbabilityMap(probabilityMapSlice);

                // mutates probability map
                var rectangles = DBNet.PostProcess(probabilityMapSlice, originalImage.Width, originalImage.Height);

                vizBuilder.AddRectangles(rectangles);

                results.Add((originalImage, rectangles, vizBuilder));
            }

            data.Buffer.Dispose();
            return results;
        });
    }
}
