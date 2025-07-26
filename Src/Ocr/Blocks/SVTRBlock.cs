using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks;

public class SVTRBlock
{
    public IPropagatorBlock<(List<TextBoundary>, Image<Rgb24>, VizBuilder), (Image<Rgb24>, List<TextBoundary>, List<string>, List<double>, VizBuilder)> Target { get; }

    public SVTRBlock(InferenceSession session, OcrConfiguration config, Meter meter)
    {
        var aggregatorBlock = new AggregatorBlock<(string, double, TextBoundary, Image<Rgb24>, VizBuilder)>();
        var splitterBlock = CreateSplitterBlock(aggregatorBlock);
        var preprocessingBlock = new SVTRPreprocessingBlock(config.Svtr);
        var modelRunnerBlock = new SVTRModelRunnerBlock(session, config, meter);
        var postprocessingBlock = new SVTRPostprocessingBlock();
        var reconstructorBlock = CreateReconstructorBlock();

        splitterBlock.LinkTo(preprocessingBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        preprocessingBlock.Target.LinkTo(modelRunnerBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        modelRunnerBlock.Target.LinkTo(postprocessingBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        postprocessingBlock.Target.LinkTo(aggregatorBlock.InputTarget, new DataflowLinkOptions { PropagateCompletion = true });
        aggregatorBlock.OutputTarget.LinkTo(reconstructorBlock, new DataflowLinkOptions { PropagateCompletion = true });

        Target = DataflowBlock.Encapsulate(splitterBlock, reconstructorBlock);
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
