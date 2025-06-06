using System.Buffers;
using System.Numerics.Tensors;

namespace OCR.Algorithms;

public static class TensorOps
{
    /// <summary>
    /// Converts tensor layout from NHWC (batch, height, width, channels) to NCHW (batch, channels, height, width).
    /// Modifies the buffer in-place by reordering data and updating the buffer's shape metadata.
    /// </summary>
    /// <typeparam name="T">Unmanaged numeric type (float, int, byte, etc.)</typeparam>
    /// <param name="buffer">Buffer containing tensor data in NHWC layout, will be modified to NCHW layout</param>
    /// <exception cref="ArgumentException">Thrown when tensor is not 4-dimensional</exception>
    /// <remarks>Performance: block-based memory-access pattern for cache efficiency</remarks>
    public static void NhwcToNchw<T>(Buffer<T> buffer) where T : unmanaged
    {
        var tensor = buffer.AsTensor();
        var shape = tensor.Lengths;  // NHWC

        if (shape.Length != 4)
        {
            throw new ArgumentException($"Tensor has {shape.Length} dimensions, expected 4 (NHWC)");
        }

        // We only need temporary space equal to the size of the data being reordered (H * W * C)
        int tempBufferSize = (int)(shape[1] * shape[2] * shape[3]);
        T[] tempBuffer = ArrayPool<T>.Shared.Rent(tempBufferSize);
        var tempTensor = Tensor.Create(tempBuffer, [shape[3], shape[1], shape[2]]);

        try
        {
            // Convert from NHWC to NCHW, one batch at a time
            for (int n = 0; n < shape[0]; n++)  // batch
            {
                for (int h = 0; h < shape[1]; h++)          // height
                    for (int w = 0; w < shape[2]; w++)      // width
                        for (int c = 0; c < shape[3]; c++)  // channel
                        {
                            tempTensor[[c, h, w]] = tensor[[n, h, w, c]];
                        }

                tempTensor.FlattenTo(buffer.AsSpan().Slice(n * tempBufferSize, tempBufferSize));
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
