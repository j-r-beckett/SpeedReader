using System.Buffers;
using Microsoft.Extensions.DependencyInjection;
using SpeedReader.Ocr.InferenceEngine;
using SpeedReader.Resources.CharDict;
using SpeedReader.Resources.Weights;

namespace BenchmarkUtils;

public sealed class KernelContext : IDisposable
{
    private readonly ArrayPool<float> _pool = ArrayPool<float>.Create();
    private int _baseInputSize;

    public required NativeOnnxInferenceKernel Kernel { get; init; }
    public required int[] InputShape { get; init; }
    public required ServiceProvider ServiceProvider { get; init; }

    private int BaseInputSize => _baseInputSize != 0 ? _baseInputSize : (_baseInputSize = InputShape.Aggregate(1, (a, b) => a * b));

    public void Infer(int batchSize = 1)
    {
        var inputSize = batchSize * BaseInputSize;
        var inputData = _pool.Rent(inputSize);
        try
        {
            var batchedShape = new[] { batchSize }.Concat(InputShape).ToArray();
            Kernel.Execute(new Memory<float>(inputData, 0, inputSize), batchedShape);
        }
        finally
        {
            _pool.Return(inputData);
        }
    }

    public void Dispose()
    {
        Kernel.Dispose();
        ServiceProvider.Dispose();
    }
}

public static class InferenceKernelFactory
{
    public static KernelContext Create(
        Model model,
        int intraThreads = 1,
        int interThreads = 1,
        bool profile = false)
    {
        var inputShape = model switch
        {
            Model.DbNet => new[] { 3, 640, 640 },
            Model.Svtr => new[] { 3, 48, 160 },
            _ => throw new ArgumentOutOfRangeException(nameof(model))
        };

        var quantization = model == Model.DbNet ? Quantization.Int8 : Quantization.Fp32;
        var weights = model == Model.DbNet ? EmbeddedWeights.Dbnet_Int8 : EmbeddedWeights.Svtr_Fp32;
        var kernelOptions = new OnnxInferenceKernelOptions(model, quantization, intraThreads, interThreads, profile);

        var services = new ServiceCollection();
        services.AddKeyedSingleton(model, kernelOptions);
        services.AddKeyedSingleton(model, weights);
        if (model == Model.Svtr)
            services.AddSingleton(new EmbeddedCharDict());
        var serviceProvider = services.BuildServiceProvider();

        var kernel = NativeOnnxInferenceKernel.Factory(serviceProvider, model);

        return new KernelContext
        {
            Kernel = kernel,
            InputShape = inputShape,
            ServiceProvider = serviceProvider
        };
    }
}
