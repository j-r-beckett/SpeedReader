using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;

namespace Ocr.Blocks;

public class InferenceBlock
{
    public IPropagatorBlock<float[], float[]> Target;

    public InferenceBlock(InferenceSession onnxSession, nint[] elementShape, Meter meter, string modelPrefix, bool cacheFirstInference = false)
    {
        Debug.Assert(elementShape.Length == 3, "Each element should have three dimensions");

        // Turn batching on here
        var batchBlock = new AdaptiveEagerBatchBlock<float[]>(1, -1);

        var inferenceTimeHistogram = meter.CreateHistogram<double>($"{modelPrefix}_inference_time_ms", "ms", $"Time spent in {modelPrefix.ToUpper()} ONNX inference");
        var batchSizeHistogram = meter.CreateHistogram<int>($"{modelPrefix}_inference_batch_size", description: $"Number of items processed in each {modelPrefix.ToUpper()} inference batch");
        var itemsProcessedCounter = meter.CreateCounter<long>($"{modelPrefix}_inference_items_processed", description: $"Total number of items processed through {modelPrefix.ToUpper()} inference");

        // Cache state for first inference result
        float[]? cachedData = null;
        long[]? cachedShape = null;

        var preprocessingBlock = new TransformBlock<float[][], OrtValue>(inputs =>
        {
            Debug.Assert(inputs.Length > 0);
            int itemSize = inputs[0].Length;
            float[] result = new float[inputs.Length * itemSize];
            for (int i = 0; i < inputs.Length; i++)
            {
                Debug.Assert(inputs[i].Length == itemSize, "All inputs should be the same length");
                inputs[i].CopyTo(result, i * itemSize);
            }

            return OrtValue.CreateTensorValueFromMemory(result, [inputs.Length, elementShape[0], elementShape[1], elementShape[2]]);
        });

        var modelRunnerBlock = new TransformBlock<OrtValue, OrtValue>(input =>
        {
            var batchSize = (int)input.GetTensorTypeAndShape().Shape[0];
            batchSizeHistogram.Record(batchSize);

            var stopwatch = Stopwatch.StartNew();

            OrtValue output;

            if (cachedData != null)
            {
                float[] outputData = (float[])cachedData.Clone();
                long[] outputShape = (long[])cachedShape!.Clone();
                output = OrtValue.CreateTensorValueFromMemory(outputData, outputShape);
            }
            else
            {
                // Create output tensor by calling onnxSession.Run()
                var inputs = new Dictionary<string, OrtValue> { { "input", input } };
                using var runOptions = new RunOptions();
                var outputs = onnxSession.Run(runOptions, inputs, onnxSession.OutputNames);
                output = outputs[0];

                // Cache first result if caching is enabled
                if (cacheFirstInference)
                {
                    cachedData = output.GetTensorDataAsSpan<float>().ToArray();
                    cachedShape = (long[])output.GetTensorTypeAndShape().Shape.Clone();
                }
            }

            stopwatch.Stop();
            var inferenceTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            inferenceTimeHistogram.Record(inferenceTimeMs, new KeyValuePair<string, object?>("batch_size", batchSize));

            itemsProcessedCounter.Add(batchSize);

            return output;
        });

        var postprocessingBlock = new TransformManyBlock<OrtValue, float[]>(tensor =>
        {
            long[] shape = tensor.GetTensorTypeAndShape().Shape;
            Debug.Assert(shape.Length == 3 || shape.Length == 4);

            // Span slicing requires ints, so we can have at most intMax = 2^31 - 1 floats in a batch.
            // That's only 4 Gb worth of floats, so we better check for overflow
            checked
            {
                int elementSize = (int)(shape[1] * shape[2] * (shape.Length == 4 ? shape[3] : 1));
                float[][] results = new float[shape[0]][];
                var tensorSpan = tensor.GetTensorDataAsSpan<float>();
                for (int i = 0; i < results.Length; i++)
                {
                    results[i] = new float[elementSize];
                    tensorSpan.Slice(i * elementSize, elementSize).CopyTo(results[i]);
                }

                return results;
            }
        });

        batchBlock.Target.LinkTo(preprocessingBlock, new DataflowLinkOptions { PropagateCompletion = true });
        preprocessingBlock.LinkTo(modelRunnerBlock, new DataflowLinkOptions { PropagateCompletion = true });
        modelRunnerBlock.LinkTo(postprocessingBlock, new DataflowLinkOptions { PropagateCompletion = true });

        Target = DataflowBlock.Encapsulate(batchBlock.Target, postprocessingBlock);
    }
}
