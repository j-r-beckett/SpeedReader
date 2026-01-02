// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SpeedReader.Native.Onnx;
using SpeedReader.Resources.Weights;

namespace SpeedReader.Native.Test.Onnx;

public class InferenceSessionRunTests
{
    [Fact]
    public void Run_WithDbNet_Works()
    {
        var model = EmbeddedWeights.Dbnet_Int8.Bytes;
        using var session = new InferenceSession(model);
        var inputData = new float[640 * 640 * 3];
        var outputData = new float[640 * 640];
        var input = OrtValue.Create(inputData.AsMemory(), [1, 3, 640, 640]);
        var output = OrtValue.Create(outputData.AsMemory(), [1, 640, 640]);
        session.Run(input, output);
    }

    [Fact]
    public void Run_WithSvtrv2_Works()
    {
        // SVTRv2: input [batch, 3, 48, width], output [batch, width/8, 6625]
        var model = EmbeddedWeights.Svtr_Fp32.Bytes;
        using var session = new InferenceSession(model);
        const int width = 160;
        var inputData = new float[1 * 3 * 48 * width];
        var outputData = new float[1 * (width / 8) * 6625];
        var input = OrtValue.Create(inputData.AsMemory(), [1, 3, 48, width]);
        var output = OrtValue.Create(outputData.AsMemory(), [1, width / 8, 6625]);
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

    [Fact]
    public void Run_WithNullInput_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
        {
            using var session = new InferenceSession(EmbeddedWeights.Dbnet_Int8.Bytes);
            var outputData = new float[640 * 640];
            var output = OrtValue.Create(outputData.AsMemory(), [1, 640, 640]);
            session.Run(null!, output);
        });

    [Fact]
    public void Run_WithNullOutput_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
        {
            using var session = new InferenceSession(EmbeddedWeights.Dbnet_Int8.Bytes);
            var inputData = new float[640 * 640 * 3];
            var input = OrtValue.Create(inputData.AsMemory(), [1, 3, 640, 640]);
            session.Run(input, null!);
        });

    [Fact]
    public void Run_WithCorrectSizeButWrongDimensions_ThrowsOrtException()
    {
        var model = EmbeddedWeights.Dbnet_Int8.Bytes;
        using var session = new InferenceSession(model);
        var inputData = new float[640 * 640 * 3];
        var outputData = new float[640 * 640];
        var input = OrtValue.Create(inputData.AsMemory(), [1, 3, 640, 640]);
        // Same element count (640*640), but different shape
        var output = OrtValue.Create(outputData.AsMemory(), [1, 1, 640, 640]);

        var exception = Assert.Throws<OrtException>(() => session.Run(input, output));

        Assert.Contains("Output shape mismatch", exception.Message);
    }

    [Fact]
    public void Run_Concurrently_Succeeds()
    {
        var model = EmbeddedWeights.Dbnet_Int8.Bytes;
        using var session = new InferenceSession(model);
        const int threadCount = 4;
        const int iterationsPerThread = 3;

        var exceptions = new List<Exception>();
        var threads = new Thread[threadCount];

        for (int t = 0; t < threadCount; t++)
        {
            threads[t] = new Thread(() =>
            {
                try
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        var inputData = new float[640 * 640 * 3];
                        var outputData = new float[640 * 640];
                        var input = OrtValue.Create(inputData.AsMemory(), [1, 3, 640, 640]);
                        var output = OrtValue.Create(outputData.AsMemory(), [1, 640, 640]);
                        session.Run(input, output);
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            });
        }

        foreach (var thread in threads)
            thread.Start();

        foreach (var thread in threads)
            thread.Join();

        Assert.Empty(exceptions);
    }
}
