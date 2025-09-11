using System.IO;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using Ocr;
using Ocr.Blocks;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Video;

public class VideoOcrBlock
{
    public ISourceBlock<(Image<Rgb24>, OcrResult)> Source { get; init; }

    public VideoOcrBlock(OcrBlock ocrBlock, Stream video, int sampleRate)
    {
        var decoderBlock = new FfmpegDecoderBlockCreator().CreateFfmpegDecoderBlock(video, sampleRate, CancellationToken.None);

        var preprocessingBlock =
            new TransformBlock<Image<Rgb24>, (Image<Rgb24>, VizBuilder)>(img => (img, new VoidVizBuilder(img)));

        var splitBlock =
            new SplitBlock<(Image<Rgb24> Img, OcrResult Result, VizBuilder Viz), OcrResult, Image<Rgb24>>(item =>
                (item.Result, item.Img));

        var deduplicatorBlock = new DeduplicatorBlock(1);  // TODO: make configurable

        var mergeBlock =
            new MergeBlock<OcrResult, Image<Rgb24>, (Image<Rgb24>, OcrResult)>((result, img) => (img, result));

        decoderBlock.LinkTo(preprocessingBlock, new DataflowLinkOptions { PropagateCompletion = true });
        preprocessingBlock.LinkTo(ocrBlock.Block, new DataflowLinkOptions { PropagateCompletion = true });
        ocrBlock.Block.LinkTo(splitBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        splitBlock.LeftSource.LinkTo(deduplicatorBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        deduplicatorBlock.Source.LinkTo(mergeBlock.LeftTarget, new DataflowLinkOptions { PropagateCompletion = true });
        splitBlock.RightSource.LinkTo(mergeBlock.RightTarget, new DataflowLinkOptions { PropagateCompletion = true });

        Source = mergeBlock.Source;
    }
}
