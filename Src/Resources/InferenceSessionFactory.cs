// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.ML.OnnxRuntime;

namespace Resources;

public class InferenceSessionFactory
{
    private readonly ResourceAccessor _resourceAccessor = new();

    public InferenceSession CreateCpuInferenceSession(Model model, Quantization quantization, int intraOpThreads, bool enableProfiling = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(intraOpThreads, 1, nameof(intraOpThreads));

        var modelBytes = GetModelBytes(model, quantization);

        // By default:
        // - execution mode is ORT_SEQUENTIAL
        // - memory arena is enabled
        // - all graph optimizations are enabled
        var options = new SessionOptions
        {
            IntraOpNumThreads = intraOpThreads,
            InterOpNumThreads = 1,
            EnableProfiling = enableProfiling
        };

        return new InferenceSession(modelBytes, options);
    }

    private byte[] GetModelBytes(Model model, Quantization quantization)
    {
        var modelNameFragment = model switch
        {
            Model.DbNet => "dbnet_resnet18_fpnc_1200e_icdar2015",
            Model.Svtr => "svtrv2_base_ctc",
            _ => throw new ArgumentException($"Unknown model {model}")
        };

        var quantizationNameFragment = quantization switch
        {
            Quantization.Fp32 => "end2end.onnx",
            Quantization.Bf16 => throw new ArgumentException("BF16 quantization not supported"),
            Quantization.Int8 => "end2end_int8.onnx",
            _ => throw new ArgumentException($"Unknown quantization {quantization}")
        };

        var resourceName = $"models.{modelNameFragment}.{quantizationNameFragment}";
        try
        {
            return _resourceAccessor.GetResourceAsBytes(resourceName);
        }
        catch (ResourceNotFoundException ex)
        {
            throw new InferenceSessionInitializationException($"Unable to load weights for model {model} with quantization {quantization}", ex);
        }
    }
}

public class InferenceSessionInitializationException : Exception
{
    public InferenceSessionInitializationException(string message, Exception innerException) : base(message, innerException) { }
}

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
