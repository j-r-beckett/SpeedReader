// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Resources;

public static class Models
{
    public static byte[] GetModelBytes(Model model) => GetModelBytes(model, ModelPrecision.FP32);

    public static byte[] GetModelBytes(Model model, ModelPrecision precision)
    {
        string resourceName = GetResourceName(model, precision);
        return Resource.GetBytes(resourceName);
    }

    private static string GetResourceName(Model model, ModelPrecision precision)
    {
        string modelName = model switch
        {
            Model.DbNet18 => "dbnet_resnet18_fpnc_1200e_icdar2015",
            Model.SVTRv2 => "svtrv2_base_ctc",
            _ => throw new ArgumentException($"Unknown model {model}")
        };

        string fileName = precision switch
        {
            ModelPrecision.FP32 => "end2end.onnx",
            ModelPrecision.INT8 => "end2end_int8.onnx",
            _ => throw new ArgumentException($"Unknown precision {precision}")
        };

        return $"models.{modelName}.{fileName}";
    }
}

public enum Model
{
    DbNet18,
    SVTRv2
}

public enum ModelPrecision
{
    FP32,
    INT8
}
