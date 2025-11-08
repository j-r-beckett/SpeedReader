// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;

namespace Ocr.InferenceEngine.Kernels;

public record NullInferenceKernelOptions : InferenceKernelOptions
{
    public NullInferenceKernelOptions(Model model, Quantization quantization, int[]? expectedInputShape, int[] outputShape)
        : base(model, quantization)
    {
        ExpectedInputShape = expectedInputShape;
        OutputShape = outputShape;
    }

    public int[]? ExpectedInputShape { get; init; }
    public int[] OutputShape { get; init; }
}

public class NullInferenceKernel : IInferenceKernel
{
    private readonly int[]? _expectedInputShape;
    private readonly int[] _outputShape;

    public NullInferenceKernel([FromKeyedServices(Model.DbNet)] NullInferenceKernelOptions inferenceOptions,
        DbNetMarker _) : this(inferenceOptions) { }

    public NullInferenceKernel([FromKeyedServices(Model.Svtr)] NullInferenceKernelOptions inferenceOptions,
        SvtrMarker _) : this(inferenceOptions) { }

    private NullInferenceKernel(NullInferenceKernelOptions inferenceOptions)
    {
        _expectedInputShape = inferenceOptions.ExpectedInputShape;
        _outputShape = inferenceOptions.OutputShape;
    }

    public (float[] OutputData, int[] OutputShape) Execute(float[] data, int[] shape)
    {
        if (_expectedInputShape != null && !_expectedInputShape.SequenceEqual(shape))
        {
            var expected = string.Join(",", _expectedInputShape);
            var actual = string.Join(",", shape);
            throw new UnexpectedInputShapeException($"Expected input shape {expected} but got {actual}");
        }

        return (new float[_outputShape.Aggregate(1, (a, b) => a * b)], _outputShape);
    }
}

public class UnexpectedInputShapeException : InferenceKernelException
{
    public UnexpectedInputShapeException(string message) : base(message) { }
}
