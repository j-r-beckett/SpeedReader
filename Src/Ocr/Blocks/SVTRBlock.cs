using System.Diagnostics;
using System.Numerics.Tensors;
using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks;

public static class SVTRBlock
{
    public static IPropagatorBlock<(List<TextBoundary>, Image<Rgb24>, VizBuilder), (Image<Rgb24>, List<TextBoundary>, List<string>, List<double>, VizBuilder)> Create(InferenceSession session, SVTRv2 svtr)
    {
        var aggregatorBlock = new AggregatorBlock<(string, double, TextBoundary, Image<Rgb24>, VizBuilder)>();
        var splitterBlock = CreateSplitterBlock(aggregatorBlock);
        var preProcessingBlock = CreatePreProcessingBlock(svtr);
        var modelRunnerBlock = CreateModelRunnerBlock(session, svtr);
        var postProcessingBlock = CreatePostProcessingBlock(svtr);
        var reconstructorBlock = CreateReconstructorBlock();

        splitterBlock.LinkTo(preProcessingBlock, new DataflowLinkOptions { PropagateCompletion = true });
        preProcessingBlock.LinkTo(modelRunnerBlock, new DataflowLinkOptions { PropagateCompletion = true });
        modelRunnerBlock.LinkTo(postProcessingBlock, new DataflowLinkOptions { PropagateCompletion = true });
        postProcessingBlock.LinkTo(aggregatorBlock.InputTarget, new DataflowLinkOptions { PropagateCompletion = true });
        aggregatorBlock.OutputTarget.LinkTo(reconstructorBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return DataflowBlock.Encapsulate(splitterBlock, reconstructorBlock);
    }

    private static TransformManyBlock<(List<TextBoundary>, Image<Rgb24>, VizBuilder), (TextBoundary, Image<Rgb24>, VizBuilder)> CreateSplitterBlock(AggregatorBlock<(string, double, TextBoundary, Image<Rgb24>, VizBuilder)> aggregatorBlock)
    {
        return new TransformManyBlock<(List<TextBoundary> TextBoundaries, Image<Rgb24> Image, VizBuilder VizBuilder), (TextBoundary, Image<Rgb24>, VizBuilder)>(async input =>
        {
            // Send batch size to aggregator
            await aggregatorBlock.BatchSizeTarget.SendAsync(input.TextBoundaries.Count);
            
            var results = new List<(TextBoundary, Image<Rgb24>, VizBuilder)>();
            foreach (var boundary in input.TextBoundaries)
            {
                results.Add((boundary, input.Image, input.VizBuilder));
            }
            return results;
        });
    }

    private static TransformBlock<(TextBoundary, Image<Rgb24>, VizBuilder), (float[], TextBoundary, Image<Rgb24>, VizBuilder)> CreatePreProcessingBlock(SVTRv2 svtr)
    {
        return new TransformBlock<(TextBoundary TextBoundary, Image<Rgb24> Image, VizBuilder VizBuilder), (float[], TextBoundary, Image<Rgb24>, VizBuilder)>(input
            => (svtr.PreProcess(input.Image, input.TextBoundary), input.TextBoundary, input.Image, input.VizBuilder));
    }

    private static TransformBlock<(float[], TextBoundary, Image<Rgb24>, VizBuilder), (float[], TextBoundary, Image<Rgb24>, VizBuilder)> CreateModelRunnerBlock(InferenceSession session, SVTRv2 svtr)
    {
        return new TransformBlock<(float[] ProcessedRegion, TextBoundary TextBoundary, Image<Rgb24> OriginalImage, VizBuilder VizBuilder), (float[], TextBoundary, Image<Rgb24>, VizBuilder)>(input =>
        {
            // Model input should be [1, 3, height, width] - single rectangle, 3 channels, configured dimensions
            var inputTensor = Tensor.Create(input.ProcessedRegion, [1, 3, svtr.Height, svtr.Width]);

            var outputBuffer = ModelRunner.Run(session, inputTensor);

            float[] outputData = outputBuffer.AsSpan().ToArray();
            outputBuffer.Dispose();

            return (outputData, input.TextBoundary, input.OriginalImage, input.VizBuilder);
        });
    }

    private static TransformBlock<(float[], TextBoundary, Image<Rgb24>, VizBuilder), (string, double, TextBoundary, Image<Rgb24>, VizBuilder)> CreatePostProcessingBlock(SVTRv2 svtr)
    {
        return new TransformBlock<(float[] RawResult, TextBoundary TextBoundary, Image<Rgb24> OriginalImage, VizBuilder VizBuilder), (string, double, TextBoundary, Image<Rgb24>, VizBuilder)>(input =>
        {
            var (recognizedText, confidence) = svtr.PostProcess(input.RawResult);

            // Add individual recognition result using thread-safe method
            input.VizBuilder.AddRecognitionResult(recognizedText, input.TextBoundary);

            return (recognizedText, confidence, input.TextBoundary, input.OriginalImage, input.VizBuilder);
        });
    }

    private static TransformBlock<(string, double, TextBoundary, Image<Rgb24>, VizBuilder)[], (Image<Rgb24>, List<TextBoundary>, List<string>, List<double>, VizBuilder)> CreateReconstructorBlock()
    {
        return new TransformBlock<(string Text, double Confidence, TextBoundary TextBoundary, Image<Rgb24> Image, VizBuilder VizBuilder)[], (Image<Rgb24>, List<TextBoundary>, List<string>, List<double>, VizBuilder)>(inputArray =>
        {
            if (inputArray.Length == 0)
            {
                throw new InvalidOperationException("Empty array received in reconstructor block");
            }

            // Extract shared references (all items should have the same Image and VizBuilder)
            var image = inputArray[0].Image;
            var vizBuilder = inputArray[0].VizBuilder;

            // Extract individual results
            var texts = new List<string>();
            var confidences = new List<double>();
            var boundaries = new List<TextBoundary>();

            foreach (var item in inputArray)
            {
                // Verify all items share the same Image and VizBuilder references
                Debug.Assert(ReferenceEquals(item.Image, image), "All items must share the same Image reference");
                Debug.Assert(ReferenceEquals(item.VizBuilder, vizBuilder), "All items must share the same VizBuilder reference");
                
                texts.Add(item.Text);
                confidences.Add(item.Confidence);
                boundaries.Add(item.TextBoundary);
            }

            return (image, boundaries, texts, confidences, vizBuilder);
        });
    }
}
