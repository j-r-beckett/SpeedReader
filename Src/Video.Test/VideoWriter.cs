// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Video.Test;

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

        // Create pipe for feeding frames to FFmpeg
        var pipeOptions = new PipeOptions(
            pauseWriterThreshold: 1024,    // Pause when 1KB in buffer
            resumeWriterThreshold: 512     // Resume when below 512 bytes
        );
        var inputPipe = new Pipe(pipeOptions);
        var streamingSource = new StreamingPipeSource(inputPipe.Reader.AsStream());

        // Write frames to pipe
        var frameWriterTask = FramesToStream(frames, inputPipe.Writer, cancellationToken);

        // Run FFmpeg with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30)); // 30 second timeout for test video creation

        var ffmpegTask = Cli.Wrap("ffmpeg")
            .WithArguments($"-f rawvideo -pix_fmt rgb24 -s {width}x{height} -r {frameRate} -i pipe:0 -c:v libvpx -crf 30 -b:v 100k -speed 16 -threads 1 -y {tempFile}")
            .WithStandardInputPipe(streamingSource)
            .WithValidation(CommandResultValidation.None) // Handle validation ourselves
            .ExecuteAsync(cts.Token);

        await Task.WhenAll(ffmpegTask, frameWriterTask);

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
