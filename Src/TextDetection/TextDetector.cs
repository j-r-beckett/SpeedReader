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
        return new TextDetectorOutput();
    }

    public void Dispose()
    {
        _session.Dispose();
        GC.SuppressFinalize(this);
    }
}
