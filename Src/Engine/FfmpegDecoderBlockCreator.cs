using System.Text;
using System.Threading.Tasks.Dataflow;
using CliWrap;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Engine;

public class FfmpegDecoderBlockCreator
{
    private readonly string _binaryPath;
    
    public FfmpegDecoderBlockCreator(string binaryPath)
    {
        _binaryPath = binaryPath;
    }
    
    public ISourceBlock<Image<Rgb24>> CreateFfmpegDecoderBlock(Stream videoData, int sampleRate, CancellationToken cancellationToken)
    {
        var source = new BufferBlock<Image<Rgb24>>();

        Task.Run(async () =>
        {
            try
            {
                var (width, height) = await new FFProbe().GetVideoDimensions(videoData, cancellationToken);
                videoData.Seek(0, SeekOrigin.Begin);

                var stdout = new MemoryStream();
                var stderr = new MemoryStream();

                var ffmpegTask = Cli.Wrap(_binaryPath)
                    .WithArguments(
                        $"-i pipe:0 -vf select=not(mod(n\\,{sampleRate})) -vsync vfr -f rawvideo -pix_fmt rgb24 pipe:1")
                    .WithStandardInputPipe(PipeSource.FromStream(videoData))
                    .WithStandardOutputPipe(PipeTarget.ToStream(stdout))
                    .WithStandardErrorPipe(PipeTarget.ToStream(stderr))
                    .ExecuteAsync();
                
                await ffmpegTask;

                // await Task.Delay(5000);
                //
                // var stderrTxt = Encoding.ASCII.GetString(stderr.ToArray());
                // var stdoutTxt = Encoding.ASCII.GetString(stdout.ToArray());
                
                var frameBuffer = new Memory<byte>(new byte[width * height * 3]);

                stdout.Position = 0;
                
                while (stdout.Position < stdout.Length)
                {
                    // Read an entire frame from the stream
                    var bytesRead = 0;
                    while (!cancellationToken.IsCancellationRequested && bytesRead < frameBuffer.Length)
                    {                
                        bytesRead += await stdout.ReadAsync(frameBuffer[bytesRead..], cancellationToken);
                    }
                    
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        if (bytesRead != frameBuffer.Length)
                        {
                            throw new Exception(
                                $"Corrupted frame, only read {bytesRead} bytes out of needed {frameBuffer.Length} bytes");
                        }
                        
                        // Load frame data into an ImageSharp image (creates a copy)
                        var frame = Image.LoadPixelData<Rgb24>(frameBuffer.Span, width, height);
                        while (!await source.SendAsync(frame, cancellationToken))
                        {
                            await Task.Delay(10, cancellationToken);
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                source.Complete();
                return;
            }
            catch (Exception ex)
            {
                ((IDataflowBlock) source).Fault(ex);
                return;
            }

            source.Complete();
        }, cancellationToken);

        return source;
    }
}