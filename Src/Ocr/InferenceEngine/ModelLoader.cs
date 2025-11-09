// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Ocr.InferenceEngine.Kernels;
using Resources;
using Model = Ocr.InferenceEngine.Kernels.Model;

namespace Ocr.InferenceEngine;

public class ModelLoader
{
    // May throw ModelNotFoundException
    public byte[] LoadModel(Model model, Quantization quantization)
    {
        var resourceName = (model, quantization) switch
        {
            (Model.DbNet, Quantization.Fp32) => "models.dbnet_resnet18_fpnc_1200e_icdar2015.end2end.onnx",
            (Model.DbNet, Quantization.Int8) => "models.dbnet_resnet18_fpnc_1200e_icdar2015.end2end_int8.onnx",
            (Model.Svtr, Quantization.Fp32) => "models.svtrv2_base_ctc.end2end.onnx",
            _ => throw new ModelNotFoundException($"{model} quantized to {quantization} is not supported")
        };
        try
        {
            return new Resource(resourceName).Bytes;
        }
        catch (ResourceNotFoundException ex)
        {
            throw new ModelNotFoundException($"Failed to load the embedded resource for {model} quantized to {quantization}", ex);
        }
    }
}

public class ModelNotFoundException : Exception
{
    public ModelNotFoundException(string message) : base(message) { }
    public ModelNotFoundException(string message, ResourceNotFoundException inner) : base(message, inner) { }
}
