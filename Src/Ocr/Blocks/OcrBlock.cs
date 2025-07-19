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
        System.Diagnostics.Metrics.Meter meter)
    {
        var dbNetBlock = DBNetBlock.Create(dbnetSession, meter);
        var svtrBlock = SVTRBlock.Create(svtrSession);
        var postProcessingBlock = OcrPostProcessingBlock.Create(meter);

        dbNetBlock.LinkTo(svtrBlock, new DataflowLinkOptions { PropagateCompletion = true });
        svtrBlock.LinkTo(postProcessingBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return DataflowBlock.Encapsulate(dbNetBlock, postProcessingBlock);
    }
}
