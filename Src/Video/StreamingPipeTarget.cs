using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;

namespace Video;

public class StreamingPipeTarget : PipeTarget
{
    private readonly Pipe _pipe;
    public PipeReader Reader => _pipe.Reader;

    public StreamingPipeTarget(PipeOptions? options = null)
    {
        var pipeOptions = options ?? new PipeOptions(
            pauseWriterThreshold: 1024,    // Pause when 1KB in buffer
            resumeWriterThreshold: 512     // Resume when below 512 bytes
        );
        _pipe = new Pipe(pipeOptions);
    }

    public override async Task CopyFromAsync(Stream origin, CancellationToken cancellationToken = default)
    {
        try
        {
            await origin.CopyToAsync(_pipe.Writer.AsStream(), cancellationToken);
        }
        finally
        {
            await _pipe.Writer.CompleteAsync();
        }
    }
}
