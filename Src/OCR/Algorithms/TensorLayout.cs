using System.Buffers;
using System.Numerics.Tensors;

namespace OCR.Algorithms;

public static class TensorLayout
{
    /// <summary>
    /// Converts tensor from NHWC layout to NCHW layout using ArrayPool buffer.
    /// </summary>
    public static void NHWCToNCHW(Tensor<float> nhwcTensor, Tensor<float> nchwTensor)
    {
        var nhwcShape = nhwcTensor.Lengths;
        var nchwShape = nchwTensor.Lengths;
        
        int batchSize = (int)nhwcShape[0];
        int height = (int)nhwcShape[1];
        int width = (int)nhwcShape[2];
        int channels = (int)nhwcShape[3];
        
        // Verify shapes are compatible (skip check if same tensor for in-place conversion)
        if (!ReferenceEquals(nhwcTensor, nchwTensor))
        {
            var targetShape = nchwTensor.Lengths;
            if (targetShape[0] != batchSize || targetShape[1] != channels || 
                targetShape[2] != height || targetShape[3] != width)
            {
                throw new ArgumentException("Tensor shapes are not compatible for NHWC to NCHW conversion");
            }
        }
        
        // Use ArrayPool buffer for intermediate conversion
        var bufferSize = batchSize * height * width * channels;
        var buffer = ArrayPool<float>.Shared.Rent(bufferSize);
        try
        {
            var bufferSpan = buffer.AsSpan(0, bufferSize);
            
            // Copy from NHWC tensor to buffer, converting to NCHW layout
            var nhwcSpan = nhwcTensor.AsTensorSpan();
            int bufferIndex = 0;
            
            for (int n = 0; n < batchSize; n++)
            {
                for (int c = 0; c < channels; c++)
                {
                    for (int h = 0; h < height; h++)
                    {
                        for (int w = 0; w < width; w++)
                        {
                            ReadOnlySpan<NRange> nhwcRange = [
                                new NRange(n, n + 1),     // Batch
                                new NRange(h, h + 1),     // Height
                                new NRange(w, w + 1),     // Width
                                new NRange(c, c + 1)      // Channel
                            ];
                            
                            ReadOnlySpan<nint> nhwcIndices = [n, h, w, c];
                            bufferSpan[bufferIndex++] = nhwcSpan[nhwcIndices];
                        }
                    }
                }
            }
            
            // Reshape tensor to NCHW layout and copy from buffer
            ReadOnlySpan<nint> reshapeToNCHW = [batchSize, channels, height, width];
            var reshapedTensor = nchwTensor.Reshape(reshapeToNCHW);
            var nchwSpan = reshapedTensor.AsTensorSpan();
            bufferIndex = 0;
            
            for (int n = 0; n < batchSize; n++)
            {
                for (int c = 0; c < channels; c++)
                {
                    for (int h = 0; h < height; h++)
                    {
                        for (int w = 0; w < width; w++)
                        {
                            ReadOnlySpan<nint> nchwIndices = [n, c, h, w];
                            nchwSpan[nchwIndices] = bufferSpan[bufferIndex++];
                        }
                    }
                }
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(buffer);
        }
    }
}