using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;
using CliWrap.Exceptions;

namespace Engine;

public class FFProbe
{
    private readonly string _binaryPath;

    public FFProbe(string binaryPath = "ffprobe")
    {
        _binaryPath = binaryPath;
    }

    public async Task<(int Width, int Height)> GetVideoDimensions(Stream video,
        CancellationToken cancellationToken)
    {
        BufferedCommandResult result;
        try
        {
            result = await RunFFProbeCommand(video, cancellationToken);
        }
        catch (CommandExecutionException ex)
        {
            throw new FFPRobeException("FFProbe failed", ex);
        }

        // Match two comma separated numbers surrounded by optional whitespace, ignoring leading zeros
        var match = Regex.Match(result.StandardOutput, @"^\s*0*(\d+),0*(\d+)\s*$");
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, out var width) || width <= 0
            || !int.TryParse(match.Groups[2].Value, out var height) || height <= 0)
        {
            throw new FFPRobeException($"Unable to parse output {result.StandardOutput}");
        }

        return (width, height);
    }

    // Protected virtual for testing
    protected virtual Task<BufferedCommandResult> RunFFProbeCommand(Stream video, CancellationToken cancellationToken)
        => Cli.Wrap(_binaryPath)
            .WithArguments("-v quiet -print_format csv=p=0 -show_entries stream=width,height pipe:0")
            .WithStandardInputPipe(PipeSource.FromStream(video))
            .ExecuteBufferedAsync(cancellationToken);
}

public class FFPRobeException : Exception
{
    public FFPRobeException(string message) : base(message) { }
    public FFPRobeException(string message, Exception inner) : base(message, inner) { }
}