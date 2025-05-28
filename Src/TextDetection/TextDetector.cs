using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;

namespace TextDetection;

public class TextDetector : IDisposable
{
    private readonly InferenceSession _session;
    private readonly ILogger<TextDetector> _logger;

    public TextDetector(InferenceSession session, ILogger<TextDetector> logger)
    {
        _session = session;
        _logger = logger;
    }

    public TextDetectorOutput RunTextDetection(TextDetectorInput input)
    {
        var inputs = new Dictionary<string, OrtValue>
        {
            { "input", input.Tensor }
        };

        using var runOptions = new RunOptions();
        using var outputs = _session.Run(runOptions, inputs, _session.OutputNames);

        var outputTensor = outputs[0];
        var outputSpan = outputTensor.GetTensorDataAsSpan<float>();
        var shape = outputTensor.GetTensorTypeAndShape().Shape;

        // Output shape is [batch_size, height, width]
        int height = (int)shape[1];
        int width = (int)shape[2];

        var probabilityMap = new float[height, width];

        for (int h = 0; h < height; h++)
        {
            for (int w = 0; w < width; w++)
            {
                probabilityMap[h, w] = outputSpan[h * width + w];
            }
        }

        return new TextDetectorOutput { ProbabilityMap = probabilityMap };
    }

    public void Dispose()
    {
        _session.Dispose();
        GC.SuppressFinalize(this);
    }
}
