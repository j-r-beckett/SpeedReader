// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ocr.InferenceEngine.Engines;
using Resources;
using Resources.Weights;

namespace Ocr.InferenceEngine;

public static class Factories
{
    public static IServiceCollection AddInferenceEngine(
        this IServiceCollection services,
        CpuEngineConfig config)
    {
        var key = config.Kernel.Model;

        services.TryAddKeyedSingleton(key, GetModelWeights(config.Kernel.Model, config.Kernel.Quantization));

        services.AddKeyedSingleton(key, config.Kernel);
        services.AddKeyedSingleton<IInferenceKernel>(key, NativeOnnxInferenceKernel.Factory);

        services.AddKeyedSingleton(key, config);
        services.AddKeyedSingleton<IInferenceEngine>(key, CpuEngine.Factory);

        return services;
    }

    private static EmbeddedWeights GetModelWeights(Model model, Quantization quantization)
    {
        try
        {
            return (model, quantization) switch
            {
                (Model.DbNet, Quantization.Fp32) => EmbeddedWeights.Dbnet_Fp32,
                (Model.DbNet, Quantization.Int8) => EmbeddedWeights.Dbnet_Int8,
                (Model.Svtr, Quantization.Fp32) => EmbeddedWeights.Svtr_Fp32,
                _ => throw new UnsupportedModelException($"{model} quantized to {quantization} is not supported")
            };
        }
        catch (ResourceNotFoundException ex)
        {
            throw new UnsupportedModelException($"Failed to load the embedded resource for {model} quantized to {quantization}", ex);
        }
    }
}

public class UnsupportedModelException : Exception
{
    public UnsupportedModelException(string message) : base(message) { }
    public UnsupportedModelException(string message, ResourceNotFoundException inner) : base(message, inner) { }
}
