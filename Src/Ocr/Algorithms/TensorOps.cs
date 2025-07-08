using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Numerics.Tensors;

namespace Ocr.Algorithms;

public static class TensorOps
{
    // Pool with maxArrayLength of 1 Gb (bigger than the ArrayPool.Shared maxArrayLength of 1 Mb)
    private static ArrayPool<float> s_pool = ArrayPool<float>.Create(1 << 30, 64);

    /// <summary>
    /// Converts tensor layout from NHWC (batch, height, width, channels) to NCHW (batch, channels, height, width).
    /// Mutates the input array in place.
    /// </summary>
    /// <param name="tensor">Array containing tensor in NHWC layout, will be mutated to NCHW layout</param>
    /// <param name="shape">Shape in NHWC format [N, H, W, C]</param>
    /// <exception cref="ArgumentException">Thrown when shape is not 4-dimensional</exception>
    /// <remarks>Performance: block-based memory-access pattern for cache efficiency</remarks>
    public static void NhwcToNchw(float[] tensor, nint[] shape)
    {
        if (shape.Length != 4)
        {
            throw new ArgumentException($"Shape has {shape.Length} dimensions, expected 4 (NHWC)");
        }

        int N = Convert.ToInt32(shape[0]);
        int H = Convert.ToInt32(shape[1]);
        int W = Convert.ToInt32(shape[2]);
        int C = Convert.ToInt32(shape[3]);

        // We only need temporary space equal to the size of the tensor being reordered (H * W * C)
        int workspaceSize;
        checked
        {
            workspaceSize = H * W * C;
        }
        float[] workspace = s_pool.Rent(workspaceSize);  // Array returned by pool may be bigger than workspaceSize!

        try
        {
            for (int n = 0; n < N; n++)
            {
                var tensorSlice = tensor.AsSpan().Slice(n * workspaceSize, workspaceSize);

                // Write HWC slice of tensor to workspace, converting to CHW as we go
                for (int h = 0; h < H; h++)          // height
                    for (int w = 0; w < W; w++)      // width
                        for (int c = 0; c < C; c++)  // channel
                        {
                            int hwcIndex = h * W * C + w * C + c;
                            int chwIndex = c * H * W + h * W + w;
                            workspace[chwIndex] = tensorSlice[hwcIndex];
                        }

                // Copy workspace back to tensor slice
                workspace.AsSpan()[.. workspaceSize].CopyTo(tensorSlice);
            }
        }
        finally
        {
            s_pool.Return(workspace);
        }
    }
}
