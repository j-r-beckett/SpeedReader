using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;



namespace TextDetection;

public class TextDetectorInput : IDisposable
{
    public OrtValue Tensor => _tensor;
    private readonly OrtValue _tensor;
    private readonly int _maxBatchSize;
    private readonly int _height;
    private readonly int _width;

    public TextDetectorInput(int maxBatchSize, int height, int width)
    {
        long[] shape = [maxBatchSize, 3, height, width];  // 3 channels because rgb24
        _tensor = OrtValue.CreateAllocatedTensorValue(OrtAllocator.DefaultInstance, TensorElementType.Float, shape);
        _maxBatchSize = maxBatchSize;
        _height = height;
        _width = width;
    }

    public void LoadBatch(params DbNetImage[] preprocessedImages)
    {
        if (preprocessedImages.Length > _maxBatchSize)
        {
            throw new OversizedBatchException(
                $"Batch size {preprocessedImages.Length} exceeds maximum {_maxBatchSize}");
        }

        var tensorSpan = _tensor.GetTensorMutableDataAsSpan<float>();
        int imageSizeBytes = _width * _height * 3;

        for (int i = 0; i < preprocessedImages.Length; i++)
        {
            var image = preprocessedImages[i];

            if (image.Width != _width || image.Height != _height)
            {
                throw new MalformedInputException(
                    $"Received image of size {image.Width}x{image.Height}, expected size is {_width}x{_height}");
            }

            image.Data.CopyTo(tensorSpan[(i * imageSizeBytes)..]);
        }

        tensorSpan[(preprocessedImages.Length * imageSizeBytes)..].Clear();
    }

    public void Dispose()
    {
        _tensor.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class OversizedBatchException : ArgumentException
{
    public OversizedBatchException(string message) : base(message) { }
}

public class MalformedInputException : ArgumentException
{
    public MalformedInputException(string message) : base(message) { }
}
