// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SpeedReader.Native.Onnx;
using SpeedReader.Resources.Weights;

namespace SpeedReader.Native.Test.Onnx;

public class InferenceSessionConstructionTests
{
    [Fact]
    public void CreateSession_WithInvalidModelData_ThrowsOrtException()
    {
        byte[] invalidModelData = [0x00, 0x01, 0x02, 0x03];

        var exception = Assert.Throws<OrtException>(() => new InferenceSession(invalidModelData));

        Assert.Contains("protobuf parsing failed", exception.Message);
    }

    [Fact]
    public void CreateSession_WithEmptyModelData_ThrowsOrtException()
    {
        byte[] invalidModelData = [];

        var exception = Assert.Throws<OrtException>(() => new InferenceSession(invalidModelData));

        Assert.Contains("invalid argument", exception.Message);
    }

    [Fact]
    public void CreateSession_WithNullModelData_ThrowsArgumentNullException() => Assert.Throws<ArgumentNullException>(() => new InferenceSession(null!));

    [Fact]
    public void CreateSession_WithNullOptions_Succeeds()
    {
        var modelData = EmbeddedWeights.Dbnet_Int8.Bytes;
        var session = new InferenceSession(modelData, options: null);
        Assert.NotNull(session);
    }

    [Fact]
    public void CreateSession_WithProfiling_Succeeds()
    {
        var modelData = EmbeddedWeights.Dbnet_Int8.Bytes;
        var session = new InferenceSession(modelData, new SessionOptions().WithProfiling());
        Assert.NotNull(session);
    }

    [Fact]
    public void CreateSession_WithThreads_Succeeds()
    {
        var modelData = EmbeddedWeights.Dbnet_Int8.Bytes;
        var options = new SessionOptions()
            .WithInterOpThreads(3)
            .WithIntraOpThreads(4);
        var session = new InferenceSession(modelData, options);
        Assert.NotNull(session);
    }
}
