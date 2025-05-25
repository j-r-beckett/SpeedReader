using System.IO.Pipelines;
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
        var source = new BufferBlock<Image<Rgb24>>(new DataflowBlockOptions
        {
            BoundedCapacity = 2 // Very small capacity to trigger backpressure immediately
        });

        Task.Run(async () =>
        {
            try
            {
                var (width, height) = await new FFProbe().GetVideoDimensions(videoData, cancellationToken);
                videoData.Seek(0, SeekOrigin.Begin);

                var streamingTarget = new StreamingPipeTarget();

                var ffmpegTask = Cli.Wrap(_binaryPath)
                    .WithArguments(
                        $"-i pipe:0 -vf select=not(mod(n\\,{sampleRate})) -vsync vfr -f rawvideo -pix_fmt rgb24 pipe:1")
                    .WithStandardInputPipe(new StreamingPipeSource(videoData))
                    .WithStandardOutputPipe(streamingTarget)
                    .ExecuteAsync();

                var frameProcessingTask = ProcessFramesAsync(streamingTarget.Reader, source, width, height, cancellationToken);

                await Task.WhenAll(ffmpegTask, frameProcessingTask);
                
                source.Complete();
            }
            catch (TaskCanceledException)
            {
                source.Complete();
            }
            catch (Exception ex)
            {
                ((IDataflowBlock)source).Fault(ex);
            }
        }, cancellationToken);

        return source;
    }

    private async Task ProcessFramesAsync(PipeReader reader, BufferBlock<Image<Rgb24>> source, int width, int height, CancellationToken cancellationToken)
    {
        var frameSize = width * height * 3;
        var frameBuffer = new byte[frameSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                if (buffer.Length >= frameSize)
                {
                    // Extract one frame
                    var frameData = buffer.Slice(0, frameSize);

                    // Copy to our frame buffer - handle single or multiple segments
                    if (frameData.IsSingleSegment)
                    {
                        frameData.FirstSpan.CopyTo(frameBuffer);
                    }
                    else
                    {
                        var position = 0;
                        foreach (var segment in frameData)
                        {
                            segment.Span.CopyTo(frameBuffer.AsSpan(position));
                            position += segment.Length;
                        }
                    }

                    // Create image and send to output
                    var frame = Image.LoadPixelData<Rgb24>(frameBuffer, width, height);
                    await source.SendAsync(frame, cancellationToken);

                    // Advance the reader past this frame
                    reader.AdvanceTo(frameData.End);
                }
                else if (result.IsCompleted)
                {
                    // No more data and insufficient bytes for a complete frame
                    if (buffer.Length > 0)
                    {
                        throw new InvalidDataException($"Incomplete frame: {buffer.Length} bytes remaining, expected {frameSize}");
                    }
                    break;
                }
                else
                {
                    // Need more data, examine the full buffer again next time
                    reader.AdvanceTo(buffer.Start, buffer.End);
                }
            }
        }
        finally
        {
            await reader.CompleteAsync();
        }
    }
}