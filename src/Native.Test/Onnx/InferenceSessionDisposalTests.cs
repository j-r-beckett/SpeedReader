// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SpeedReader.Native.Onnx;
using SpeedReader.Resources.Weights;

namespace SpeedReader.Native.Test.Onnx;

public class InferenceSessionDisposalTests
{
    [Fact]
    public void Dispose_Works()
    {
        var model = EmbeddedWeights.Dbnet_Int8.Bytes;
        var session = new InferenceSession(model);
        session.Dispose();
    }

    [Fact]
    public void Dispose_Twice_Works()
    {
        var model = EmbeddedWeights.Dbnet_Int8.Bytes;
        var session = new InferenceSession(model);
        session.Dispose();
        session.Dispose();
    }

    [Fact]
    public void Run_AfterDisposal_ThrowsObjectDisposedException()
    {
        var model = EmbeddedWeights.Dbnet_Int8.Bytes;
        var session = new InferenceSession(model);
        session.Dispose();
        Assert.Throws<ObjectDisposedException>(() => session.Run(null!, null!));
    }
}
