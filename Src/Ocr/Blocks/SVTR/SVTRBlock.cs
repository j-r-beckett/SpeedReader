// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks.SVTR;

public class SVTRBlock
{
    public IPropagatorBlock<(List<TextBoundary>, Image<Rgb24>, VizData?), (Image<Rgb24>, List<TextBoundary>, List<string>, List<double>, VizData?)> Target { get; }

    public SVTRBlock(InferenceSession session, OcrConfiguration config, Meter meter)
    {
        // Split input to separate SVTR processing data from VizData
        var inputSplitBlock = new SplitBlock<(List<TextBoundary> TextBoundaries, Image<Rgb24> Image, VizData? VizData), (List<TextBoundary> TextBoundaries, Image<Rgb24> Image), VizData?>(
            input => ((input.TextBoundaries, input.Image), input.VizData));

        // Create SVTR pipeline without VizData
        var aggregatorBlock = new AggregatorBlock<(string, double, TextBoundary, Image<Rgb24>)>();
        var splitterBlock = CreateSplitterBlock(aggregatorBlock);
        var preprocessingBlock = new SVTRPreprocessingBlock(config.Svtr);
        var modelRunnerBlock = new SVTRModelRunnerBlock(session, config, meter);
        var postprocessingBlock = new SVTRPostprocessingBlock();
        var reconstructorBlock = CreateReconstructorBlock();

        splitterBlock.LinkTo(preprocessingBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        preprocessingBlock.Target.LinkTo(modelRunnerBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        modelRunnerBlock.Target.LinkTo(postprocessingBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        postprocessingBlock.Target.LinkTo(aggregatorBlock.InputTarget, new DataflowLinkOptions { PropagateCompletion = true });
        aggregatorBlock.OutputTarget.LinkTo(reconstructorBlock, new DataflowLinkOptions { PropagateCompletion = true });

        var svtrPipeline = DataflowBlock.Encapsulate(splitterBlock, reconstructorBlock);

        // TODO: once max parallelism is made configurable, use the appropriate value here
        var (anabranchStartBlock, anabranchBufferBlock) = CreateAnabranchStartBlock(svtrPipeline, 10);
        var anabranchEndBlock = CreateAnabranchEndBlock(svtrPipeline, anabranchBufferBlock);

        var svtrWithAnabranch = DataflowBlock.Encapsulate(anabranchStartBlock, anabranchEndBlock);

        // Merge SVTR results back with VizData
        var outputMergeBlock = new MergeBlock<(Image<Rgb24> Image, List<TextBoundary> TextBoundaries, List<string> Texts, List<double> Confidences), VizData?, (Image<Rgb24> Image, List<TextBoundary> TextBoundaries, List<string> Texts, List<double> Confidences, VizData? VizData)>(
            (svtrResult, vizData) => (svtrResult.Image, svtrResult.TextBoundaries, svtrResult.Texts, svtrResult.Confidences, vizData));

        // Wire up the split/merge pattern
        inputSplitBlock.LeftSource.LinkTo(svtrWithAnabranch, new DataflowLinkOptions { PropagateCompletion = true });
        inputSplitBlock.RightSource.LinkTo(outputMergeBlock.RightTarget, new DataflowLinkOptions { PropagateCompletion = true });
        svtrWithAnabranch.LinkTo(outputMergeBlock.LeftTarget, new DataflowLinkOptions { PropagateCompletion = true });

        Target = DataflowBlock.Encapsulate(inputSplitBlock.Target, outputMergeBlock.Source);
    }

    private static (ITargetBlock<(List<TextBoundary>, Image<Rgb24>)>, BufferBlock<Image<Rgb24>?>) CreateAnabranchStartBlock(
        ITargetBlock<(List<TextBoundary>, Image<Rgb24>)> svtrPipeline, int svtrPipelineMaxParallelism)
    {
        // We need to put a backpressure limit here so that we don't blow up under a deluge of images without detected text.
        // But we want that backpressure limit to be strictly higher than the max parallelism of the svtr flow, or else
        // it will artificially limit it.
        var buffer = new BufferBlock<Image<Rgb24>?>(new DataflowBlockOptions
        {
            BoundedCapacity = svtrPipelineMaxParallelism * 2
        });

        var anabranchStartBlock = new TransformBlock<(List<TextBoundary> TextBoundaries, Image<Rgb24> Image), Image<Rgb24>?>(async input =>
        {
            if (input.TextBoundaries.Count == 0)
            {
                return input.Image;
            }

            await svtrPipeline.SendAsync(input);
            return null;
        }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });

        anabranchStartBlock.LinkTo(buffer, new DataflowLinkOptions { PropagateCompletion = true });

        return (anabranchStartBlock, buffer);
    }

    private static ISourceBlock<(Image<Rgb24>, List<TextBoundary>, List<string>, List<double>)> CreateAnabranchEndBlock(
        ISourceBlock<(Image<Rgb24>, List<TextBoundary>, List<string>, List<double>)> svtrPipeline,
        BufferBlock<Image<Rgb24>?> buffer)
    {
        Channel<(Image<Rgb24>, List<TextBoundary>, List<string>, List<double>)> svtrChannel = Channel.CreateBounded<(Image<Rgb24>, List<TextBoundary>, List<string>, List<double>)>(1);

        var svtrChannelFeeder = new ActionBlock<(Image<Rgb24>, List<TextBoundary>, List<string>, List<double>)>(async input => await svtrChannel.Writer.WriteAsync(input), new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });

        svtrPipeline.LinkTo(svtrChannelFeeder, new DataflowLinkOptions { PropagateCompletion = true });

        var anabranchEndBlock = new TransformBlock<Image<Rgb24>?, (Image<Rgb24>, List<TextBoundary>, List<string>, List<double>)>(async input =>
        {
            if (input != null)
            {
                return (input, [], [], []);
            }

            await svtrChannel.Reader.WaitToReadAsync();
            return svtrChannel.Reader.TryRead(out var result)
                ? result
                : throw new UnexpectedFlowException($"Race condition in {nameof(svtrChannel)}");
        }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });

        buffer.LinkTo(anabranchEndBlock, new DataflowLinkOptions { PropagateCompletion = true });

        svtrChannelFeeder.Completion.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                ((IDataflowBlock)anabranchEndBlock).Fault(t.Exception);
            }
            else
            {
                anabranchEndBlock.Complete();
            }
        });

        return anabranchEndBlock;
    }


    private static TransformManyBlock<(List<TextBoundary>, Image<Rgb24>), (TextBoundary, Image<Rgb24>)> CreateSplitterBlock(AggregatorBlock<(string, double, TextBoundary, Image<Rgb24>)> aggregatorBlock) => new TransformManyBlock<(List<TextBoundary> TextBoundaries, Image<Rgb24> Image), (TextBoundary, Image<Rgb24>)>(async input =>
                                                                                                                                                                                                                   {
                                                                                                                                                                                                                       // Send batch size to aggregator
                                                                                                                                                                                                                       await aggregatorBlock.BatchSizeTarget.SendAsync(input.TextBoundaries.Count);

                                                                                                                                                                                                                       var results = new List<(TextBoundary, Image<Rgb24>)>();
                                                                                                                                                                                                                       foreach (var boundary in input.TextBoundaries)
                                                                                                                                                                                                                       {
                                                                                                                                                                                                                           results.Add((boundary, input.Image));
                                                                                                                                                                                                                       }
                                                                                                                                                                                                                       return results;
                                                                                                                                                                                                                   }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });

    private static TransformBlock<(string, double, TextBoundary, Image<Rgb24>)[], (Image<Rgb24>, List<TextBoundary>, List<string>, List<double>)> CreateReconstructorBlock() => new TransformBlock<(string Text, double Confidence, TextBoundary TextBoundary, Image<Rgb24> Image)[], (Image<Rgb24>, List<TextBoundary>, List<string>, List<double>)>(inputArray =>
                                                                                                                                                                                     {
                                                                                                                                                                                         if (inputArray.Length == 0)
                                                                                                                                                                                         {
                                                                                                                                                                                             throw new InvalidOperationException("Empty array received in reconstructor block");
                                                                                                                                                                                         }

                                                                                                                                                                                         // Extract shared references (all items should have the same Image)
                                                                                                                                                                                         var image = inputArray[0].Image;

                                                                                                                                                                                         // Extract individual results
                                                                                                                                                                                         var texts = new List<string>();
                                                                                                                                                                                         var confidences = new List<double>();
                                                                                                                                                                                         var boundaries = new List<TextBoundary>();

                                                                                                                                                                                         foreach (var item in inputArray)
                                                                                                                                                                                         {
                                                                                                                                                                                             // Verify all items share the same Image reference
                                                                                                                                                                                             Debug.Assert(ReferenceEquals(item.Image, image), "All items must share the same Image reference");

                                                                                                                                                                                             texts.Add(item.Text);
                                                                                                                                                                                             confidences.Add(item.Confidence);
                                                                                                                                                                                             boundaries.Add(item.TextBoundary);
                                                                                                                                                                                         }

                                                                                                                                                                                         return (image, boundaries, texts, confidences);
                                                                                                                                                                                     }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });

    public class UnexpectedFlowException : Exception
    {
        public UnexpectedFlowException(string message) : base(message) { }
    }
}
