using System.Diagnostics;
using System.Threading.Channels;
using FFMpegCore;
using FFMpegCore.Exceptions;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using VideoInfo = FFMpegCore.VideoStream;

namespace Engine;

// Extracts video frames from a video stream using FFMpeg
// Writes frames to an output channel for further processing
// Caller is responsible for returning frames to memory pool
public class FrameReader
{
    private readonly ILogger _logger;

    public FrameReader(ILogger logger)
    {
        _logger = logger;
        GlobalFFOptions.Configure(new FFOptions { BinaryFolder = "/usr/bin", TemporaryFilesFolder = "/tmp" });
        var nsPerTick = 1000L * 1000L * 1000L / Stopwatch.Frequency;
        _logger.LogInformation("Ticks per second: {freq}, ns per tick: {ns}, is high resolution: {b}", 
            Stopwatch.Frequency, nsPerTick, Stopwatch.IsHighResolution);
    }

    // Reads frames from a video stream and outputs them to a channel at the specified frame rate
    // Parameters:
    // - videoStream: Input video stream to process
    // - memoryPool: Pool for reusing memory buffers for frame data, caller is responsible for returning buffers to the pool
    // - outFramesPerSecond: Desired output frame rate
    // - outChannel: Channel to receive frames
    // - cancellationToken: Token to cancel the operation
    public async Task ReadFramesAsync(
        Stream videoStream,
        int outFramesPerSecond,
        Channel<Image<Rgb24>> outChannel,
        CancellationToken cancellationToken)
    {
        if (outFramesPerSecond <= 0)
        {
            throw new FrameReaderException($"Output frame rate of {outFramesPerSecond} must be a positive number");
        }

        var videoInfo = await GetVideoInfoAsync(videoStream, cancellationToken);

        var frameWidth = videoInfo.Width;
        var frameHeight = videoInfo.Height;

        var everyNthFrame = Math.Round(videoInfo.FrameRate / outFramesPerSecond);

        // Launch an FFMpeg process that produces raw video frames in rgb24 format
        // This is done entirely in-memory, nothing is written to disk
        try
        {
            _logger.LogInformation("Starting ffmpeg");

            var pipeSource = new SwitchableStreamPipeSource(videoStream);
            
            var ffmpegTask = FFMpegArguments.FromPipeInput(pipeSource)
                .OutputToPipe(new StreamPipeSink(RawFrameWriter), options => options
                    .ForceFormat("rawvideo") // Output raw pixel data
                    .WithCustomArgument($"-vf select=not(mod(n\\,{everyNthFrame})) -vsync vfr") // Only output every nth frame
                    .UsingThreads(1) // Limits FFMpeg process to single thread for predictable performance
                    .ForcePixelFormat("rgb24"))
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously();

            const int targetNumUnprocessedFrames = 50;
            
            while (!ffmpegTask.IsCompleted)
            {
                var unprocessed = outChannel.Reader.Count;

                if (unprocessed > 2 * targetNumUnprocessedFrames)
                {
                    pipeSource.SwitchOff();
                }
                else if (unprocessed < targetNumUnprocessedFrames)
                {
                    pipeSource.SwitchOn();
                }

                await Task.Delay(500, cancellationToken);
            }

            await ffmpegTask;
            
            _logger.LogInformation("Finished ffmpeg");
        }
        catch (FFMpegException ex)
        {
            _logger.LogInformation("ffmpeg error");
            throw new FrameReaderException("Unable to extract frames with FFMpeg", ex);
        }
        finally
        {
            // All frames should be written by the time we reach this point
            outChannel.Writer.Complete();
        }
        
        cancellationToken.ThrowIfCancellationRequested();
        
        _logger.LogInformation("Frame reader is peacefully returning");

        return;

        // Reads raw video frames from a byte stream and writes them to the output channel
        // Memory<byte> version
        async Task RawFrameWriter(Stream inputStream, CancellationToken writerCancellationToken)
        {
            // Create a buffer big enough to store one frame (width * height * 3 bytes per pixel)
            var frameBuffer = new Memory<byte>(new byte[frameWidth * frameHeight * 3]);

            // The input stream is assumed to be unbounded, this method returns only when cancellation is requested
            while (!writerCancellationToken.IsCancellationRequested)
            {
                // Read an entire frame from the stream
                var bytesRead = 0;
                while (!writerCancellationToken.IsCancellationRequested && bytesRead < frameBuffer.Length)
                {                
                    bytesRead += await inputStream.ReadAsync(frameBuffer[bytesRead..], cancellationToken);
                }

                if (!writerCancellationToken.IsCancellationRequested)
                {
                    if (bytesRead != frameBuffer.Length)
                    {
                        throw new FrameReaderException(
                            $"Corrupted frame, only read {bytesRead} bytes out of needed {frameBuffer.Length} bytes");
                    }
                    // Load frame data into an ImageSharp image and write it to the output channel
                    await outChannel.Writer.WriteAsync(Image.LoadPixelData<Rgb24>(frameBuffer.Span, frameWidth, frameHeight), cancellationToken);
                }
            }
        }
    }

    // Extract video metadata while preserving stream position
    private static async Task<VideoInfo> GetVideoInfoAsync(Stream videoStream, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
        // const int timeoutSeconds = 2;  // long enough for a network request
        // var position = videoStream.Position;
        // IMediaAnalysis analysis;
        // try
        // {
        //     // FFProbe cancellation is bugged, so do a manual timeout instead of passing a cancellation token
        //     analysis = await VideoUtils.AnalyseAsync(videoStream, cancellationToken: default)
        //         .WaitAsync(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
        // }
        // catch (TimeoutException exception)
        // {
        //     throw new FrameReaderException($"FFProbe analysis timed out after {timeoutSeconds} seconds", exception);
        // }
        // catch (FFMpegException exception)
        // {
        //     throw new FrameReaderException("Unable to analyze video stream with FFProbe", exception);
        // }
        // finally
        // {
        //     videoStream.Seek(position, SeekOrigin.Begin);  // Restore stream position after analysis
        // }
        // return analysis.PrimaryVideoStream ?? throw new FrameReaderException("Null value returned by FFProbe");
    }

    // Custom exception for FrameReader-specific errors
    public class FrameReaderException : Exception
    {
        public FrameReaderException(string message) : base(message) { }
        public FrameReaderException(string message, Exception inner) : base(message, inner) { }
    }
    
    public class SwitchableStreamPipeSource : IPipeSource
    {
        private readonly Stream _sourceStream;
        private const int BufferSize = 4096;
        private TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();
        private bool _isSet = true;
        private readonly object _lock = new ();

        public SwitchableStreamPipeSource(Stream sourceStream)
        {
            _sourceStream = sourceStream;
            _tcs.SetResult(true); // Initially signaled (ON)
        }

        public string GetStreamArguments() => string.Empty;

        public void SwitchOn()
        {
            lock (_lock)
            {
                if (!_isSet)
                {
                    _tcs.SetResult(true);
                    _isSet = true;
                }
            }
        }
    
        public void SwitchOff()
        {
            lock (_lock)
            {
                if (_isSet)
                {
                    _tcs = new TaskCompletionSource<bool>();
                    _isSet = false;
                }
            }
        } 

        public async Task WriteAsync(Stream outputStream, CancellationToken cancellationToken)
        {
            var buffer = new byte[BufferSize];
            int bytesRead;
            while ((bytesRead = await _sourceStream.ReadAsync(new Memory<byte>(buffer), cancellationToken)) != 0)
            {
                await _tcs.Task;
                await outputStream.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancellationToken);
            }
        }
    }
}