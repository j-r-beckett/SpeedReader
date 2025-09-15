// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics.Metrics;
using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks.SVTR;

public class SVTRModelRunnerBlock
{
    public IPropagatorBlock<(float[], TextBoundary, Image<Rgb24>), (float[], TextBoundary, Image<Rgb24>)> Target { get; }

    public SVTRModelRunnerBlock(InferenceSession session, OcrConfiguration config, Meter meter)
    {
        var splitBlock = new SplitBlock<(float[], TextBoundary, Image<Rgb24>), float[], (TextBoundary, Image<Rgb24>)>(
            input => (input.Item1, (input.Item2, input.Item3)));

        var inferenceBlock = new InferenceBlock(session, [3, config.Svtr.Height, config.Svtr.Width], meter, "svtr", config.CacheFirstInference);

        var mergeBlock = new MergeBlock<float[], (TextBoundary, Image<Rgb24>), (float[], TextBoundary, Image<Rgb24>)>(
            (result, passthrough) => (result, passthrough.Item1, passthrough.Item2));

        splitBlock.LeftSource.LinkTo(inferenceBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        splitBlock.RightSource.LinkTo(mergeBlock.RightTarget, new DataflowLinkOptions { PropagateCompletion = true });
        inferenceBlock.Target.LinkTo(mergeBlock.LeftTarget, new DataflowLinkOptions { PropagateCompletion = true });

        Target = DataflowBlock.Encapsulate(splitBlock.Target, mergeBlock.Source);
    }
}
