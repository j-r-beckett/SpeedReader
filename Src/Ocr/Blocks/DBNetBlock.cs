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

    private static IPropagatorBlock<(float[], Image<Rgb24>, VizBuilder), (float[], Image<Rgb24>, VizBuilder)> CreateModelRunnerBlock(InferenceSession session)
    {
        var splitBlock = new SplitBlock<(float[], Image<Rgb24>, VizBuilder), float[], (Image<Rgb24>, VizBuilder)>(
            input => (input.Item1, (input.Item2, input.Item3)));

        var inferenceBlock = new InferenceBlock(session, [3, 736, 1344]);

        var mergeBlock = new MergeBlock<float[], (Image<Rgb24>, VizBuilder), (float[], Image<Rgb24>, VizBuilder)>(
            (result, passthrough) => 
            {
                passthrough.Item2.AddProbabilityMap(result.AsSpan().AsSpan2D(736, 1344));
                return (result, passthrough.Item1, passthrough.Item2);
            });

        splitBlock.LeftSource.LinkTo(inferenceBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        splitBlock.RightSource.LinkTo(mergeBlock.RightTarget, new DataflowLinkOptions { PropagateCompletion = true });
        inferenceBlock.Target.LinkTo(mergeBlock.LeftTarget, new DataflowLinkOptions { PropagateCompletion = true });

        return DataflowBlock.Encapsulate(splitBlock.Target, mergeBlock.Source);
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
