// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.ML.OnnxRuntime;
using Ocr.InferenceEngine;

namespace Ocr.Test.InferenceEngine;

public class ModelLoaderTests
{
    [Theory]
    [InlineData(Model.DbNet, Quantization.Fp32)]
    [InlineData(Model.DbNet, Quantization.Int8)]
    [InlineData(Model.Svtr, Quantization.Fp32)]
    public void LoadModel_ReturnsValidOnnxModel(Model model, Quantization quantization)
    {
        // Arrange
        var loader = new ModelLoader();

        // Act
        var modelBytes = loader.LoadModel(model, quantization);

        // Assert
        Assert.NotNull(modelBytes);
        Assert.True(modelBytes.Length > 0);

        using var session = new InferenceSession(modelBytes);
        Assert.NotNull(session);
    }

    [Theory]
    [InlineData(Model.Svtr, Quantization.Int8)]
    [InlineData(Model.Svtr, Quantization.Bf16)]
    [InlineData(Model.DbNet, Quantization.Bf16)]
    public void LoadModel_UnsupportedModelQuantization_ThrowsModelNotFoundException(Model model, Quantization quantization)
    {
        // Arrange
        var loader = new ModelLoader();

        // Act & Assert
        var exception = Assert.Throws<ModelNotFoundException>(() => loader.LoadModel(model, quantization));
        Assert.Contains($"{model} quantized to {quantization} is not supported", exception.Message);
    }
}
