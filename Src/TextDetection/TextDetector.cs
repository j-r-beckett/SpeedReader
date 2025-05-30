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

    public IDisposableReadOnlyCollection<OrtValue> RunTextDetection(OrtValue input)
    {
        var inputs = new Dictionary<string, OrtValue>
        {
            { "input", input }
        };

        using var runOptions = new RunOptions();
        return _session.Run(runOptions, inputs, _session.OutputNames);
    }


    public void Dispose()
    {
        _session.Dispose();
        GC.SuppressFinalize(this);
    }
}
