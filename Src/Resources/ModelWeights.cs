// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Resources;

public class ModelWeights
{
    private readonly Resource _resource;

    private ModelWeights(string resourceName) => _resource = new Resource(resourceName);

    public static ModelWeights Dbnet_Fp32 = new("models.dbnet_resnet18_fpnc_1200e_icdar2015_fp32.onnx");

    public static ModelWeights Dbnet_Int8 = new("models.dbnet_resnet18_fpnc_1200e_icdar2015_int8.onnx");

    public static ModelWeights Svtr_Fp32 = new("models.svtrv2_base_ctc_fp32.onnx");

    public byte[] Bytes => _resource.Bytes;
}
