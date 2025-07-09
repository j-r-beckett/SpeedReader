using System.Threading.Tasks.Dataflow;
using System.Numerics.Tensors;
using Microsoft.ML.OnnxRuntime;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks;

public static class SVTRBlock
{
    public static IPropagatorBlock<(List<Rectangle>, Image<Rgb24>, VizBuilder), (Image<Rgb24>, List<Rectangle>, List<string>, VizBuilder)> Create(InferenceSession session)
    {
        var preProcessingBlock = CreatePreProcessingBlock();
        var modelRunnerBlock = CreateModelRunnerBlock(session);
        var postProcessingBlock = CreatePostProcessingBlock();

        preProcessingBlock.LinkTo(modelRunnerBlock, new DataflowLinkOptions { PropagateCompletion = true });
        modelRunnerBlock.LinkTo(postProcessingBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return DataflowBlock.Encapsulate(preProcessingBlock, postProcessingBlock);
    }


    private static TransformBlock<(List<Rectangle>, Image<Rgb24>, VizBuilder), (float[], List<Rectangle>, Image<Rgb24>, VizBuilder)> CreatePreProcessingBlock()
    {
        return new TransformBlock<(List<Rectangle> Rectangles, Image<Rgb24> Image, VizBuilder VizBuilder), (float[], List<Rectangle>, Image<Rgb24>, VizBuilder)>(input
            => (SVTRv2.PreProcess(input.Image, input.Rectangles), input.Rectangles, input.Image, input.VizBuilder));
    }

    private static TransformBlock<(float[], List<Rectangle>, Image<Rgb24>, VizBuilder), (float[], List<Rectangle>, Image<Rgb24>, VizBuilder)> CreateModelRunnerBlock(InferenceSession session)
    {
        return new TransformBlock<(float[] ProcessedRegions, List<Rectangle> Rectangles, Image<Rgb24> OriginalImage, VizBuilder VizBuilder), (float[], List<Rectangle>, Image<Rgb24>, VizBuilder)>(input =>
        {
            // Model input should be [num_rectangles, 3, 48, 320] - rectangles, 3 channels, height 48, width 320
            int numRectangles = input.Rectangles.Count;
            var inputTensor = Tensor.Create(input.ProcessedRegions, [ numRectangles, 3, 48, 320 ]);

            var outputBuffer = ModelRunner.Run(session, inputTensor);

            float[] outputData = outputBuffer.AsSpan().ToArray();
            outputBuffer.Dispose();

            return (outputData, input.Rectangles, input.OriginalImage, input.VizBuilder);
        });
    }

    private static TransformBlock<(float[], List<Rectangle>, Image<Rgb24>, VizBuilder), (Image<Rgb24>, List<Rectangle>, List<string>, VizBuilder)> CreatePostProcessingBlock()
    {
        return new TransformBlock<(float[] RawResult, List<Rectangle> Rectangles, Image<Rgb24> OriginalImage, VizBuilder VizBuilder), (Image<Rgb24>, List<Rectangle>, List<string>, VizBuilder)>(input =>
        {
            var recognizedTexts = SVTRv2.PostProcess(input.RawResult, input.Rectangles.Count);

            input.VizBuilder.AddRecognitionResults(recognizedTexts.ToList());

            return (input.OriginalImage, input.Rectangles, recognizedTexts.ToList(), input.VizBuilder);
        });
    }
}
