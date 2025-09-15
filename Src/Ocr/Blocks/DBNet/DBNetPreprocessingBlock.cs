// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Numerics.Tensors;
using System.Threading.Tasks.Dataflow;
using Ocr.Algorithms;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks.DBNet;

public class DBNetPreprocessingBlock
{
    private readonly int _width;
    private readonly int _height;

    public IPropagatorBlock<(Image<Rgb24>, VizBuilder), (float[], Image<Rgb24>, VizBuilder)> Target { get; }

    public DBNetPreprocessingBlock(DbNetConfiguration config)
    {
        _width = config.Width;
        _height = config.Height;

        Target = new TransformBlock<(Image<Rgb24> Image, VizBuilder VizBuilder), (float[], Image<Rgb24>, VizBuilder)>(input
            => (PreProcess(input.Image), input.Image, input.VizBuilder), new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 1
            });
    }

    private float[] PreProcess(Image<Rgb24> image)
    {
        float[] data = new float[_height * _width * 3];

        // Resize
        Resampling.AspectResizeInto(image, data, _width, _height);

        // Convert to CHW format
        TensorOps.NhwcToNchw(data, [_height, _width, 3]);

        // Apply ImageNet normalization
        float[] means = [123.675f, 116.28f, 103.53f];
        float[] stds = [58.395f, 57.12f, 57.375f];

        for (int channel = 0; channel < 3; channel++)
        {
            var tensor = Tensor.Create(data, channel * _height * _width, [_height, _width], default);

            // Subtract mean and divide by std in place
            Tensor.Subtract(tensor, means[channel], tensor);
            Tensor.Divide(tensor, stds[channel], tensor);
        }

        return data;
    }
}
