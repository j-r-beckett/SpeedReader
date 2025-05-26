using System;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using CliWrap;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Engine;

public class FfmpegEncoderBlockCreator
{
    private readonly string _binaryPath;

    public FfmpegEncoderBlockCreator(string binaryPath)
    {
        _binaryPath = binaryPath;
    }

    public ITargetBlock<Image<Rgb24>> CreateFfmpegEncoderBlock(
        int width,
        int height,
        double frameRate,
        out PipeReader encodedOutput,
        CancellationToken cancellationToken)
    {
        // Output: Use existing StreamingPipeTarget for FFmpeg stdout -> PipeReader
        var streamingTarget = new StreamingPipeTarget();
        encodedOutput = streamingTarget.Reader;

        // Input: Create Pipe for frames -> FFmpeg stdin with same thresholds as StreamingPipeTarget
        var pipeOptions = new PipeOptions(
            pauseWriterThreshold: 1024,    // Pause when 1KB in buffer
            resumeWriterThreshold: 512     // Resume when below 512 bytes
        );
        var inputPipe = new Pipe(pipeOptions);
        var streamingSource = new StreamingPipeSource(inputPipe.Reader.AsStream());

        // ActionBlock processes frames and writes to inputPipe.Writer
        var targetBlock = new ActionBlock<Image<Rgb24>>(
            async frame => await WriteFrameToPipeAsync(frame, inputPipe.Writer, width, height, cancellationToken),
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 2, // Same as decoder for immediate backpressure
                CancellationToken = cancellationToken
            });

        // Start FFmpeg process in background
        Task.Run(async () =>
        {
            try
            {
                // Wait for target block completion, then close input
                var completionTask = targetBlock.Completion.ContinueWith(async _ =>
                {
                    await inputPipe.Writer.CompleteAsync();
                });

                var ffmpegTask = Cli.Wrap(_binaryPath)
                    .WithArguments($"-f rawvideo -pix_fmt rgb24 -s {width}x{height} -r {frameRate} -i pipe:0 -c:v libvpx -crf 30 -b:v 100k -speed 16 -threads 1 -f webm pipe:1")
                    .WithStandardInputPipe(streamingSource)
                    .WithStandardOutputPipe(streamingTarget)
                    .ExecuteAsync(cancellationToken);

                await Task.WhenAll(ffmpegTask, completionTask);
            }
            catch (TaskCanceledException)
            {
                // Expected on cancellation
            }
            catch (Exception ex)
            {
                // Fault the target block on process errors
                ((IDataflowBlock)targetBlock).Fault(ex);
            }
            finally
            {
                // Ensure input pipe is completed
                await inputPipe.Writer.CompleteAsync();
            }
        }, cancellationToken);

        return targetBlock;
    }

    private static async Task WriteFrameToPipeAsync(Image<Rgb24> frame, PipeWriter writer, int width, int height, CancellationToken cancellationToken)
    {
        var frameSize = width * height * 3; // RGB24 = 3 bytes per pixel

        // Validate frame dimensions
        if (frame.Width != width || frame.Height != height)
        {
            throw new InvalidOperationException($"Frame size mismatch: expected {width}x{height}, got {frame.Width}x{frame.Height}");
        }

        var destination = writer.GetMemory(frameSize);

        // Convert Image<Rgb24> to raw bytes (same logic as original FrameWriter)
        frame.ProcessPixelRows(rowAccessor =>
        {
            for (var y = 0; y < rowAccessor.Height; y++)
            {
                var rowBytes = MemoryMarshal.AsBytes(rowAccessor.GetRowSpan(y));
                rowBytes.CopyTo(destination.Span.Slice(y * width * 3));
            }
        });

        writer.Advance(frameSize);
        var result = await writer.FlushAsync(cancellationToken);

        // This is where backpressure happens - FlushAsync blocks when FFmpeg can't keep up
        if (result.IsCompleted)
        {
            throw new InvalidOperationException("Pipe writer completed unexpectedly");
        }

        // Dispose frame immediately to free memory
        frame.Dispose();
    }
}
