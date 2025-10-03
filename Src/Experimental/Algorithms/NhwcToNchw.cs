// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Buffers;

namespace Experimental.Algorithms;

public static class NhwcToNchwExtensions
{
    // Pool with maxArrayLength of 1 Gb (bigger than the ArrayPool.Shared maxArrayLength of 1 Mb)
    private static readonly ArrayPool<float> _pool = ArrayPool<float>.Create(1 << 30, 64);

    public static void HwcToChwInPlace(this float[] tensor, nint[] shape)
    {
        if (shape.Length != 3)
        {
            throw new ArgumentException($"Shape has {shape.Length} dimensions, expected 3 (HWC)");
        }

        int H = Convert.ToInt32(shape[0]);
        int W = Convert.ToInt32(shape[1]);
        int C = Convert.ToInt32(shape[2]);

        int workspaceSize;
        checked
        {
            workspaceSize = H * W * C;
        }

        var workspaceData = _pool.Rent(workspaceSize);
        var workspace = workspaceData.AsSpan()[..workspaceSize];  // Array returned by pool may be bigger than workspaceSize

        try
        {
            // Write tensor to workspace, converting to CHW as we go
            for (int h = 0; h < H; h++)
            {
                for (int w = 0; w < W; w++)
                {
                    for (int c = 0; c < C; c++)
                    {
                        int hwcIndex = h * W * C + w * C + c;
                        int chwIndex = c * H * W + h * W + w;
                        workspace[chwIndex] = tensor[hwcIndex];
                    }
                }
            }

            // Copy workspace back to tensor
            workspace.CopyTo(tensor.AsSpan());
        }
        finally
        {
            _pool.Return(workspaceData);
        }
    }
}
