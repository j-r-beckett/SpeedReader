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
    public IPropagatorBlock<(List<TextBoundary>, Image<Rgb24>, VizBuilder), (Image<Rgb24>, List<TextBoundary>, List<string>, List<double>, VizBuilder)> Target { get; }

    public SVTRBlock(InferenceSession session, OcrConfiguration config, Meter meter)
    {
        var aggregatorBlock = new AggregatorBlock<(string, double, TextBoundary, Image<Rgb24>, VizBuilder)>();
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

        Target = DataflowBlock.Encapsulate(anabranchStartBlock, anabranchEndBlock);
    }

    private static (ITargetBlock<(List<TextBoundary>, Image<Rgb24>, VizBuilder)>, BufferBlock<(Image<Rgb24>, VizBuilder)?>) CreateAnabranchStartBlock(
        ITargetBlock<(List<TextBoundary>, Image<Rgb24>, VizBuilder)> svtrPipeline, int svtrPipelineMaxParallelism)
    {
        // We need to put a backpressure limit here so that we don't blow up under a deluge of images without detected text.
        // But we want that backpressure limit to be strictly higher than the max parallelism of the svtr flow, or else
        // it will artificially limit it.
        var buffer = new BufferBlock<(Image<Rgb24>, VizBuilder)?>(new DataflowBlockOptions
        {
            BoundedCapacity = svtrPipelineMaxParallelism * 2
        });

        var anabranchStartBlock = new TransformBlock<(List<TextBoundary> TextBoundaries, Image<Rgb24> Image, VizBuilder VizBuilder), (Image<Rgb24>, VizBuilder)?>(async input =>
        {
            if (input.TextBoundaries.Count == 0)
            {
                return (input.Image, input.VizBuilder);
            }

            await svtrPipeline.SendAsync(input);
            return null;
        }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });

        anabranchStartBlock.LinkTo(buffer, new DataflowLinkOptions { PropagateCompletion = true });

        return (anabranchStartBlock, buffer);
    }

    private static ISourceBlock<(Image<Rgb24>, List<TextBoundary>, List<string>, List<double>, VizBuilder)> CreateAnabranchEndBlock(
        ISourceBlock<(Image<Rgb24>, List<TextBoundary>, List<string>, List<double>, VizBuilder)> svtrPipeline,
        BufferBlock<(Image<Rgb24>, VizBuilder)?> buffer)
    {
        Channel<(Image<Rgb24>, List<TextBoundary>, List<string>, List<double>, VizBuilder)> svtrChannel = Channel.CreateBounded<(Image<Rgb24>, List<TextBoundary>, List<string>, List<double>, VizBuilder)>(1);

        var svtrChannelFeeder = new ActionBlock<(Image<Rgb24>, List<TextBoundary>, List<string>, List<double>, VizBuilder)>(async input =>
        {
            await svtrChannel.Writer.WriteAsync(input);
        }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });

        svtrPipeline.LinkTo(svtrChannelFeeder, new DataflowLinkOptions { PropagateCompletion = true });

        var anabranchEndBlock = new TransformBlock<(Image<Rgb24> Image, VizBuilder VizBuilder)?, (Image<Rgb24>, List<TextBoundary>, List<string>, List<double>, VizBuilder)>(async input =>
        {
            if (input != null)
            {
                return (input.Value.Image, [], [], [], input.Value.VizBuilder);
            }

            await svtrChannel.Reader.WaitToReadAsync();
            if (svtrChannel.Reader.TryRead(out var result))
            {
                return result;
            }

            throw new UnexpectedFlowException($"Race condition in {nameof(svtrChannel)}");
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


    private static TransformManyBlock<(List<TextBoundary>, Image<Rgb24>, VizBuilder), (TextBoundary, Image<Rgb24>, VizBuilder)> CreateSplitterBlock(AggregatorBlock<(string, double, TextBoundary, Image<Rgb24>, VizBuilder)> aggregatorBlock)
    {
        return new TransformManyBlock<(List<TextBoundary> TextBoundaries, Image<Rgb24> Image, VizBuilder VizBuilder), (TextBoundary, Image<Rgb24>, VizBuilder)>(async input =>
        {
            // Send batch size to aggregator
            await aggregatorBlock.BatchSizeTarget.SendAsync(input.TextBoundaries.Count);

            var results = new List<(TextBoundary, Image<Rgb24>, VizBuilder)>();
            foreach (var boundary in input.TextBoundaries)
            {
                results.Add((boundary, input.Image, input.VizBuilder));
            }
            return results;
        }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });
    }

    private static TransformBlock<(string, double, TextBoundary, Image<Rgb24>, VizBuilder)[], (Image<Rgb24>, List<TextBoundary>, List<string>, List<double>, VizBuilder)> CreateReconstructorBlock()
    {
        return new TransformBlock<(string Text, double Confidence, TextBoundary TextBoundary, Image<Rgb24> Image, VizBuilder VizBuilder)[], (Image<Rgb24>, List<TextBoundary>, List<string>, List<double>, VizBuilder)>(inputArray =>
        {
            if (inputArray.Length == 0)
            {
                throw new InvalidOperationException("Empty array received in reconstructor block");
            }

            // Extract shared references (all items should have the same Image and VizBuilder)
            var image = inputArray[0].Image;
            var vizBuilder = inputArray[0].VizBuilder;

            // Extract individual results
            var texts = new List<string>();
            var confidences = new List<double>();
            var boundaries = new List<TextBoundary>();

            foreach (var item in inputArray)
            {
                // Verify all items share the same Image and VizBuilder references
                Debug.Assert(ReferenceEquals(item.Image, image), "All items must share the same Image reference");
                Debug.Assert(ReferenceEquals(item.VizBuilder, vizBuilder), "All items must share the same VizBuilder reference");

                texts.Add(item.Text);
                confidences.Add(item.Confidence);
                boundaries.Add(item.TextBoundary);
            }

            return (image, boundaries, texts, confidences, vizBuilder);
        }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });
    }

    public class UnexpectedFlowException : Exception
    {
        public UnexpectedFlowException(string message) : base(message) { }
    }
}
