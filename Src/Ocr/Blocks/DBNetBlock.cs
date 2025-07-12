using System.Numerics.Tensors;
using System.Threading.Tasks.Dataflow;
using CommunityToolkit.HighPerformance;
using Microsoft.ML.OnnxRuntime;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks;

public static class DBNetBlock
{
    public static IPropagatorBlock<(Image<Rgb24>, VizBuilder), (List<TextBoundary>, Image<Rgb24>, VizBuilder)> Create(InferenceSession session)
    {
        var preProcessingBlock = CreatePreProcessingBlock();
        var modelRunnerBlock = CreateModelRunnerBlock(session);
        var postProcessingBlock = CreatePostProcessingBlock();

        preProcessingBlock.LinkTo(modelRunnerBlock, new DataflowLinkOptions { PropagateCompletion = true });
        modelRunnerBlock.LinkTo(postProcessingBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return DataflowBlock.Encapsulate(preProcessingBlock, postProcessingBlock);
    }


    private static TransformBlock<(Image<Rgb24>, VizBuilder), (float[], Image<Rgb24>, VizBuilder)> CreatePreProcessingBlock()
    {
        return new TransformBlock<(Image<Rgb24> Image, VizBuilder VizBuilder), (float[], Image<Rgb24>, VizBuilder)>(input
            => (DBNet.PreProcess(input.Image), input.Image, input.VizBuilder));
    }

    private static TransformBlock<(float[], Image<Rgb24>, VizBuilder), (float[], Image<Rgb24>, VizBuilder)> CreateModelRunnerBlock(InferenceSession session)
    {
        return new TransformBlock<(float[] ProcessedImage, Image<Rgb24> OriginalImage, VizBuilder VizBuilder), (float[], Image<Rgb24>, VizBuilder)>(input =>
        {
            // Model input should be [1, 3, 736, 1344] - batch size 1, 3 channels, height 736, width 1344
            // ProcessedImage is now in CHW format [3, 736, 1344], so we add batch dimension
            var inputTensor = Tensor.Create(input.ProcessedImage, [1, 3, 736, 1344]);

            var outputBuffer = ModelRunner.Run(session, inputTensor);

            // Model output should be [1, 1, 736, 1344] - single channel probability map
            float[] outputData = outputBuffer.AsSpan().ToArray();
            outputBuffer.Dispose();

            input.VizBuilder.AddProbabilityMap(outputData.AsSpan().AsSpan2D(736, 1344));

            return (outputData, input.OriginalImage, input.VizBuilder);
        });
    }

    private static TransformBlock<(float[], Image<Rgb24>, VizBuilder), (List<TextBoundary>, Image<Rgb24>, VizBuilder)> CreatePostProcessingBlock()
    {
        return new TransformBlock<(float[] RawResult, Image<Rgb24> OriginalImage, VizBuilder VizBuilder), (List<TextBoundary>, Image<Rgb24>, VizBuilder)>(input =>
        {
            // Add raw probability map to visualization BEFORE binarization
            input.VizBuilder.AddProbabilityMap(input.RawResult.AsSpan().AsSpan2D(736, 1344));

            var textBoundaries = DBNet.PostProcess(input.RawResult, input.OriginalImage.Width, input.OriginalImage.Height);

            input.VizBuilder.AddRectangles(textBoundaries.Select(tb => tb.AARectangle).ToList());
            input.VizBuilder.AddPolygons(textBoundaries.Select(tb => tb.Polygon).ToList());

            return (textBoundaries, input.OriginalImage, input.VizBuilder);
        });
    }
}
