using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;
using Ocr.Blocks.DBNet;
using Ocr.Blocks.SVTR;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks;

public class OcrBlock
{
    public readonly IPropagatorBlock<(Image<Rgb24>, VizBuilder), (Image<Rgb24>, OcrResult, VizBuilder)> Block;

    public OcrBlock(
        InferenceSession dbnetSession,
        InferenceSession svtrSession,
        OcrConfiguration config,
        System.Diagnostics.Metrics.Meter meter)
    {
        // Transform input tuple to OcrContext at the start of the pipeline
        var contextCreationBlock = new TransformBlock<(Image<Rgb24>, VizBuilder), OcrContext>(
            input => new OcrContext(input.Item1, input.Item2), new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 1
            });

        var dbNetBlock = new DBNetBlock(dbnetSession, config, meter);
        var svtrBlock = new SVTRBlock(svtrSession, config, meter);
        var postProcessingBlock = OcrPostProcessingBlock.Create(meter);

        contextCreationBlock.LinkTo(dbNetBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        dbNetBlock.Target.LinkTo(svtrBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        svtrBlock.Target.LinkTo(postProcessingBlock, new DataflowLinkOptions { PropagateCompletion = true });

        Block = DataflowBlock.Encapsulate(contextCreationBlock, postProcessingBlock);
    }
}
