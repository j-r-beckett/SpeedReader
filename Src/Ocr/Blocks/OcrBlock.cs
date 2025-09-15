// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

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
    public readonly IPropagatorBlock<(Image<Rgb24>, VizData?), (Image<Rgb24>, OcrResult, VizData?)> Block;

    public OcrBlock(
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

        Block = DataflowBlock.Encapsulate(dbNetBlock.Target, postProcessingBlock);
    }
}
