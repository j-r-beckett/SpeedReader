// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Threading.Tasks.Dataflow;
using Ocr.Algorithms;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks.SVTR;

public class SVTRPostprocessingBlock
{
    public IPropagatorBlock<(float[], TextBoundary, Image<Rgb24>), (string, double, TextBoundary, Image<Rgb24>)> Target
    {
        get;
    }

    public SVTRPostprocessingBlock() => Target = new TransformBlock<(float[] RawResult, TextBoundary TextBoundary, Image<Rgb24> OriginalImage), (string, double, TextBoundary, Image<Rgb24>)>(input =>
                                             {
                                                 var (recognizedText, confidence) = CTC.DecodeSingleSequence(input.RawResult, CharacterDictionary.Count);

                                                 return (recognizedText, confidence, input.TextBoundary, input.OriginalImage);
                                             }, new ExecutionDataflowBlockOptions
                                             {
                                                 BoundedCapacity = 1
                                             });
}
