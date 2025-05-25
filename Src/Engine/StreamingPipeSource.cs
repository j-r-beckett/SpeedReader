using CliWrap;

namespace Engine;

public class StreamingPipeSource : PipeSource
{
    private readonly Stream _sourceStream;

    public StreamingPipeSource(Stream sourceStream)
    {
        _sourceStream = sourceStream;
    }

    public override async Task CopyToAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        await _sourceStream.CopyToAsync(destination, cancellationToken);
    }
}