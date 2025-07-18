using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Microsoft.ML.OnnxRuntime;

namespace Ocr.Blocks;

public class InferenceBlock
{
    public IPropagatorBlock<float[], float[]> Target;

    public InferenceBlock(InferenceSession onnxSession, nint[] elementShape)
    {
        Debug.Assert(elementShape.Length == 3, "Each element should have three dimensions");

        var batchBlock = new AdaptiveEagerBatchBlock<float[]>(4, -1);

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
            var inputs = new Dictionary<string, OrtValue> { { "input", input } };
            using var runOptions = new RunOptions();
            var outputs = onnxSession.Run(runOptions, inputs, onnxSession.OutputNames);
            return outputs[0];  // One output b/c input dict has one element
        });

        var postprocessingBlock = new TransformManyBlock<OrtValue, float[]>(tensor =>
        {
            long[] shape = tensor.GetTensorTypeAndShape().Shape;
            Debug.Assert(shape.Length == 4);

            // Span slicing requires ints, so we can have at most intMax = 2^31 - 1 floats in a batch.
            // That's only 4 Gb worth of floats, so we better check for overflow
            checked
            {
                int elementSize = (int)(shape[1] * shape[2] * shape[3]);
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
