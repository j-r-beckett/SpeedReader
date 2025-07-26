using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks;

public static class OcrBlock
{
    public static IPropagatorBlock<(Image<Rgb24>, VizBuilder), (Image<Rgb24>, OcrResult, VizBuilder)> Create(
        InferenceSession dbnetSession,
        InferenceSession svtrSession,
        OcrConfiguration config,
        System.Diagnostics.Metrics.Meter meter)
    {
        var dbNetBlock = new DBNetBlock(dbnetSession, config, meter);
        var svtrBlock = new SVTRBlock(svtrSession, config, meter);
        var postProcessingBlock = OcrPostProcessingBlock.Create(meter);

        dbNetBlock.Target.LinkTo(svtrBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        svtrBlock.Target.LinkTo(postProcessingBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return DataflowBlock.Encapsulate(dbNetBlock.Target, postProcessingBlock);
    }
}
