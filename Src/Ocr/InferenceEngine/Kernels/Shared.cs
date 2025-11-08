// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Ocr.InferenceEngine.Kernels;

public enum Model
{
    DbNet,
    Svtr
}

public enum Quantization
{
    Int8,
    Bf16,
    Fp32
}


public interface IInferenceKernel
{
    (float[] OutputData, int[] OutputShape) Execute(float[] data, int[] shape);
}

public class InferenceKernelException : Exception
{
    public InferenceKernelException(string message) : base(message) { }
    public InferenceKernelException(string message, Exception innerException) : base(message, innerException) { }
}

public abstract record InferenceKernelOptions
{
    public InferenceKernelOptions(Model model, Quantization quantization)
    {
        Model = model;
        Quantization = quantization;
    }

    public Model Model { get; }
    public Quantization Quantization { get; }
}
