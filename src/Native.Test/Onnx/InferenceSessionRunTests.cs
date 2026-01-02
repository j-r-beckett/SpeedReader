// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SpeedReader.Native.Onnx;
using SpeedReader.Resources.Weights;

namespace SpeedReader.Native.Test.Onnx;

public class InferenceSessionRunTests
{
    [Fact]
    public void Run_WithValidInput_Works()
    {
        var model = EmbeddedWeights.Dbnet_Int8.Bytes;
        var session = new InferenceSession(model);
        var inputData = new float[640 * 640 * 3];
        var outputData = new float[640 * 640];
        var input = OrtValue.Create(inputData.AsMemory(), [1, 3, 640, 640]);
        var output = OrtValue.Create(outputData.AsMemory(), [1, 640, 640]);
        session.Run(input, output);
    }

    [Fact]
    public void Run_WithInvalidInput_Throws()
    {
        var model = EmbeddedWeights.Dbnet_Int8.Bytes;
        var session = new InferenceSession(model);
        var inputData = new float[1];
        var outputData = new float[640 * 720];
        var input = OrtValue.Create(inputData.AsMemory(), [1, 1, 1, 1]);
        var output = OrtValue.Create(outputData.AsMemory(), [1, 640, 720]);
        var exception = Assert.Throws<OrtException>(() => session.Run(input, output));
        Assert.Contains("Got invalid dimensions for input", exception.Message);
    }

    [Fact]
    public void Run_WithInvalidOutputShape_Throws()
    {
        var model = EmbeddedWeights.Dbnet_Int8.Bytes;
        var session = new InferenceSession(model);
        var inputData = new float[640 * 640 * 3];
        var outputData = new float[640 * 720];
        var input = OrtValue.Create(inputData.AsMemory(), [1, 3, 640, 640]);
        var output = OrtValue.Create(outputData.AsMemory(), [1, 640, 720]);
        var exception = Assert.Throws<OrtException>(() => session.Run(input, output));
        Assert.Contains("output size mismatch", exception.Message);
    }
}
