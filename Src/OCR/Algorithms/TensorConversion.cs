using System.Buffers;
using System.Numerics.Tensors;

namespace OCR.Algorithms;

public static class TensorConversion
{
    public static void NHWCToNCHW<T>(Buffer<T> buffer) where T : unmanaged
    {
        var tensor = buffer.AsTensor();
        var shape = tensor.Lengths;  // NHWC

        if (shape.Length != 4)
        {
            throw new ArgumentException($"Tensor has {shape.Length} dimensions, expected 4 (NHWC)");
        }

        int batchSize = (int)(shape[1] * shape[2] * shape[3]);
        T[] tempBuffer = ArrayPool<T>.Shared.Rent(batchSize);
        var tempTensor = Tensor.Create(tempBuffer, [shape[3], shape[1], shape[2]]);
        try
        {
            // Convert from NHWC to NCHW
            // TODO: refactor to a block-based memory-access pattern
            for (int n = 0; n < shape[0]; n++) // batch
            {
                for (int h = 0; h < shape[1]; h++)      // height
                    for (int w = 0; w < shape[2]; w++)      // width
                        for (int c = 0; c < shape[3]; c++)      // channel
                        {
                            tempTensor[[c, h, w]] = tensor[[n, h, w, c]];
                        }

                tempTensor.FlattenTo(buffer.AsSpan().Slice(n * batchSize, batchSize));
            }
        }
        finally
        {
            ArrayPool<T>.Shared.Return(tempBuffer);
        }

        // Update buffer's shape to NCHW
        buffer.Shape = [shape[0], shape[3], shape[1], shape[2]];
    }
}
