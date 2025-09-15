// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics.Metrics;
using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks.DBNet;

public class DBNetModelRunnerBlock
{
    public IPropagatorBlock<(float[], Image<Rgb24>, VizData?), (float[], Image<Rgb24>, VizData?)> Target { get; }

    public DBNetModelRunnerBlock(InferenceSession session, OcrConfiguration config, Meter meter)
    {
        var splitBlock = new SplitBlock<(float[], Image<Rgb24>, VizData?), float[], (Image<Rgb24>, VizData?)>(
            input => (input.Item1, (input.Item2, input.Item3)));

        var inferenceBlock = new InferenceBlock(session, [3, config.DbNet.Height, config.DbNet.Width], meter, "dbnet", config.CacheFirstInference);

        var mergeBlock = new MergeBlock<float[], (Image<Rgb24>, VizData?), (float[], Image<Rgb24>, VizData?)>(
            (result, passthrough) => (result, passthrough.Item1, passthrough.Item2));

        splitBlock.LeftSource.LinkTo(inferenceBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        splitBlock.RightSource.LinkTo(mergeBlock.RightTarget, new DataflowLinkOptions { PropagateCompletion = true });
        inferenceBlock.Target.LinkTo(mergeBlock.LeftTarget, new DataflowLinkOptions { PropagateCompletion = true });

        Target = DataflowBlock.Encapsulate(splitBlock.Target, mergeBlock.Source);
    }
}
