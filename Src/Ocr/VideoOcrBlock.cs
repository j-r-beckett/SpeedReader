// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Threading.Tasks.Dataflow;
using Ocr.Blocks;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Video;

namespace Ocr;

public class VideoOcrBlock
{
    public ISourceBlock<(Image<Rgb24>, OcrResult, VizBuilder)> Source { get; init; }

    public VideoOcrBlock(OcrBlock ocrBlock, Stream video, int sampleRate)
    {
        var decoderBlock = new FfmpegDecoderBlockCreator().CreateFfmpegDecoderBlock(video, sampleRate, CancellationToken.None);

        var preprocessingBlock =
            new TransformBlock<Image<Rgb24>, (Image<Rgb24>, VizBuilder)>(img => (img, new BasicVizBuilder(img)));

        var splitBlock =
            new SplitBlock<(Image<Rgb24> Img, OcrResult Result, VizBuilder Viz), OcrResult, (Image<Rgb24>, VizBuilder)>(item =>
                (item.Result, (item.Img, item.Viz)));

        var deduplicatorBlock = new DeduplicatorBlock(1);  // TODO: make configurable

        var mergeBlock =
            new MergeBlock<OcrResult, (Image<Rgb24>, VizBuilder), (Image<Rgb24>, OcrResult, VizBuilder)>((result, imgViz) => (imgViz.Item1, result, imgViz.Item2));

        decoderBlock.LinkTo(preprocessingBlock, new DataflowLinkOptions { PropagateCompletion = true });
        preprocessingBlock.LinkTo(ocrBlock.Block, new DataflowLinkOptions { PropagateCompletion = true });
        ocrBlock.Block.LinkTo(splitBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        splitBlock.LeftSource.LinkTo(deduplicatorBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        deduplicatorBlock.Source.LinkTo(mergeBlock.LeftTarget, new DataflowLinkOptions { PropagateCompletion = true });
        splitBlock.RightSource.LinkTo(mergeBlock.RightTarget, new DataflowLinkOptions { PropagateCompletion = true });

        Source = mergeBlock.Source;
    }
}
