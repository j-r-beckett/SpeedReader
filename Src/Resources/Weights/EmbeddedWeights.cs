// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Resources.Weights;

public class EmbeddedWeights
{
    private readonly Resource _resource;

    private EmbeddedWeights(string resourceName) => _resource = new Resource(resourceName);

    public static EmbeddedWeights Dbnet_Int8 = new("Weights.dbnet_resnet18_fpnc_1200e_icdar2015_int8.onnx");

    public static EmbeddedWeights Svtr_Fp32 = new("Weights.svtrv2_base_ctc_fp32.onnx");

    public byte[] Bytes => _resource.Bytes;
}
