using System.Diagnostics.Metrics;
using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks;

public class SVTRModelRunnerBlock
{
    public IPropagatorBlock<(float[], TextBoundary, Image<Rgb24>, VizBuilder), (float[], TextBoundary, Image<Rgb24>, VizBuilder)> Target { get; }

    public SVTRModelRunnerBlock(InferenceSession session, OcrConfiguration config, Meter meter)
    {
        var splitBlock = new SplitBlock<(float[], TextBoundary, Image<Rgb24>, VizBuilder), float[], (TextBoundary, Image<Rgb24>, VizBuilder)>(
            input => (input.Item1, (input.Item2, input.Item3, input.Item4)));

        var inferenceBlock = new InferenceBlock(session, [3, config.Svtr.Height, config.Svtr.Width], meter, "svtr", config.CacheFirstInference);

        var mergeBlock = new MergeBlock<float[], (TextBoundary, Image<Rgb24>, VizBuilder), (float[], TextBoundary, Image<Rgb24>, VizBuilder)>(
            (result, passthrough) => (result, passthrough.Item1, passthrough.Item2, passthrough.Item3));

        splitBlock.LeftSource.LinkTo(inferenceBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        splitBlock.RightSource.LinkTo(mergeBlock.RightTarget, new DataflowLinkOptions { PropagateCompletion = true });
        inferenceBlock.Target.LinkTo(mergeBlock.LeftTarget, new DataflowLinkOptions { PropagateCompletion = true });

        Target = DataflowBlock.Encapsulate(splitBlock.Target, mergeBlock.Source);
    }
}
