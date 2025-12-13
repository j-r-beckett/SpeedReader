// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Native;
using Resources.CharDict;
using Resources.Weights;

namespace Ocr.InferenceEngine;

public class NativeOnnxInferenceKernel : IInferenceKernel, IDisposable
{
    private readonly InferenceSession _session;
    private readonly Model _model;
    private readonly int _vocabSize;

    public static NativeOnnxInferenceKernel Factory(IServiceProvider serviceProvider, object? key)
    {
        var options = serviceProvider.GetRequiredKeyedService<OnnxInferenceKernelOptions>(key);
        var modelWeights = serviceProvider.GetRequiredKeyedService<EmbeddedWeights>(key);
        var charDict = serviceProvider.GetService<EmbeddedCharDict>();
        return new NativeOnnxInferenceKernel(options, modelWeights, charDict);
    }

    private NativeOnnxInferenceKernel(
        OnnxInferenceKernelOptions inferenceOptions,
        EmbeddedWeights weights,
        EmbeddedCharDict? charDict = null)
    {
        _model = inferenceOptions.Model;

        if (_model == Model.Svtr && charDict is null)
            throw new ArgumentException($"EmbeddedCharDict is required for {_model}", nameof(charDict));

        _vocabSize = charDict?.Count ?? 0;

        var sessionOptions = new SessionOptions()
            .WithIntraOpThreads(inferenceOptions.NumIntraOpThreads)
            .WithInterOpThreads(inferenceOptions.NumInterOpThreads)
            .WithProfiling(inferenceOptions.EnableProfiling);

        _session = new InferenceSession(weights.Bytes, sessionOptions);
    }

    public virtual (float[] OutputData, int[] OutputShape) Execute(float[] data, int[] shape)
    {
        var input = OrtValue.Create(data, shape);
        var outputShape = CalculateOutputShape(shape);
        var outputData = new float[outputShape.Aggregate(1, (a, b) => a * b)];
        var output = OrtValue.Create(outputData, outputShape);
        _session.Run(input, output);
        return (outputData, outputShape);
    }

    private int[] CalculateOutputShape(int[] inputShape) =>
        (_model, inputShape) switch
        {
            // DBNet: [n, 3, h, w] -> [n, h, w]
            (Model.DbNet, [var n, _, var h, var w]) => [n, h, w],
            // SVTR: [n, 3, 48, w] -> [n, w/8, vocab_size]
            (Model.Svtr, [var n, _, _, var w]) => [n, w / 8, _vocabSize],
            _ => throw new ArgumentException($"Unexpected input shape for model {_model}: [{string.Join(", ", inputShape)}]")
        };

    public void Dispose()
    {
        _session.Dispose();
        GC.SuppressFinalize(this);
    }
}
