using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks;

public static class OcrBlock
{
    public static IPropagatorBlock<Image<Rgb24>, (Image<Rgb24>, List<Rectangle>, List<string>)> Create(InferenceSession dbnetSession, InferenceSession svtrSession)
    {
        var dbNetBlock = DBNetBlock.Create(dbnetSession);
        var svtrBlock = SVTRBlock.Create(svtrSession);
        var postProcessingBlock = OcrPostProcessingBlock.Create();

        dbNetBlock.LinkTo(svtrBlock, new DataflowLinkOptions { PropagateCompletion = true });
        svtrBlock.LinkTo(postProcessingBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return DataflowBlock.Encapsulate(dbNetBlock, postProcessingBlock);
    }
}
