using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using FFMpegCore;
using FFMpegCore.Pipes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Engine;

public class FrameWriter
{
    public static async Task<Stream> ToCompressedVideo(
        int width,
        int height,
        double frameRate,
        IAsyncEnumerable<Image<Rgb24>> frames,
        CancellationToken cancellationToken)
    {
        var tempFile = Path.GetTempFileName() + ".webm";

        var pipe = new Pipe();
        var readStream = pipe.Reader.AsStream();

        var streamTask = FramesToStream(frames, pipe.Writer, cancellationToken);

        var ffmpegTask = FFMpegArguments
            .FromPipeInput(new StreamPipeSource(readStream), options => options
                .WithCustomArgument($"-f rawvideo -pixel_format rgb24 -video_size {width}x{height} -framerate {frameRate}"))
            .OutputToFile(tempFile, addArguments: options => options
                .WithCustomArgument("-vcodec libvpx -crf 30 -b:v 100k -speed 16 -threads 1"))
            .ProcessAsynchronously();

        await Task.WhenAll(ffmpegTask, streamTask);

        return new FileStream(tempFile, FileMode.Open, FileAccess.Read);
    }

    private static async Task FramesToStream<T>(IAsyncEnumerable<Image<T>> channel, PipeWriter writer, CancellationToken cancellationToken) where T : unmanaged, IPixel<T>
    {
        var bytesPerPixel = Marshal.SizeOf<T>();
        var frameSizeInBytes = -1;
        try
        {
            await foreach (var frame in channel.WithCancellation(cancellationToken))
            {
                if (frameSizeInBytes == -1)
                {
                    frameSizeInBytes = frame.Width * frame.Height * bytesPerPixel;
                }

                if (frame.Width * frame.Height * bytesPerPixel != frameSizeInBytes)
                {
                    throw new InvalidOperationException("All frames must be the same size");
                }

                var destination = writer.GetMemory(frameSizeInBytes);

                frame.ProcessPixelRows(rowAccessor =>
                {
                    for (var y = 0; y < rowAccessor.Height; y++)
                    {
                        var rowBytes = MemoryMarshal.AsBytes(rowAccessor.GetRowSpan(y));
                        rowBytes.CopyTo(destination.Span.Slice(y * frame.Width * bytesPerPixel));
                    }
                });

                writer.Advance(frameSizeInBytes);
                await writer.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            await writer.CompleteAsync();
        }
    }
}