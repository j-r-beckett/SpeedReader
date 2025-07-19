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
        var dbNet = new DBNet(config.DbNet);
        var svtr = new SVTRv2(config.Svtr);
        
        var dbNetBlock = DBNetBlock.Create(dbnetSession, dbNet, meter);
        var svtrBlock = SVTRBlock.Create(svtrSession, svtr);
        var postProcessingBlock = OcrPostProcessingBlock.Create(meter);

        dbNetBlock.LinkTo(svtrBlock, new DataflowLinkOptions { PropagateCompletion = true });
        svtrBlock.LinkTo(postProcessingBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return DataflowBlock.Encapsulate(dbNetBlock, postProcessingBlock);
    }
}
