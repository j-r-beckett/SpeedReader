# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

OpusFlow is a .NET 8 video processing solution with three main components:
- **Engine**: Core video processing library using FFMpegCore, CliWrap, and SixLabors.ImageSharp
- **Engine.Test**: Test project with video generation and processing tests
- **Engine.Benchmark**: Performance benchmarking with Chart.js visualization

## Common Commands

### Build and Test
```bash
# Build entire solution
dotnet build

# Run all tests  
dotnet test

# Run specific test with detailed output
dotnet test --logger "console;verbosity=detailed" --filter "CanDecodeRedBlueFrames"

# Run backpressure test
dotnet test --logger "console;verbosity=detailed" --filter "BackpressureStopsInputConsumption"

# Run single test project
dotnet test Src/Engine.Test/

# Clean and restore
dotnet clean
dotnet restore
```

## Architecture Notes

### Streaming Video Processing with Natural Backpressure

The core architecture uses **System.IO.Pipelines** and **CliWrap** to create a true streaming video processing pipeline with automatic backpressure control:

1. **Video Encoding**: `FrameWriter` converts image frames to compressed video (WebM/VP8) via System.IO.Pipelines
2. **Video Decoding**: `FfmpegDecoderBlockCreator` streams video frames using custom `StreamingPipeSource` and `StreamingPipeTarget` implementations
3. **Natural Backpressure**: When downstream consumers are slow, the entire pipeline automatically pauses FFmpeg without manual intervention

**Key Innovation**: FFmpeg naturally responds to backpressure. When output pipes fill up, FFmpeg automatically pauses input consumption, creating perfect end-to-end flow control.

### Streaming Architecture Components

- **StreamingPipeSource**: Custom `PipeSource` implementation that feeds video data to FFmpeg stdin with natural backpressure
- **StreamingPipeTarget**: Custom `PipeTarget` implementation that receives FFmpeg stdout via `System.IO.Pipelines.Pipe` with configurable buffer thresholds
- **FfmpegDecoderBlockCreator**: Creates `ISourceBlock<Image<Rgb24>>` that streams decoded frames with bounded capacity for backpressure
- **Concurrent Processing**: Frame decoding happens concurrently with FFmpeg processing using `Task.WhenAll()`

### Backpressure Flow Control

The backpressure chain works as follows:
1. Consumer can't keep up → `BufferBlock<Image<Rgb24>>` fills up (bounded capacity)
2. `SendAsync()` blocks in frame processing
3. `PipeReader` stops consuming → Pipe buffer fills to `pauseWriterThreshold`
4. `FlushAsync()` blocks → FFmpeg stdout blocks
5. FFmpeg automatically pauses and stops reading from stdin
6. Input stream consumption stops until downstream pressure is relieved

**Verified by test**: `BackpressureStopsInputConsumption` proves that with no consumer, a large video stream stops at exactly 131,072 bytes and remains stable, demonstrating perfect backpressure control.

### Key Components

- **FrameWriter**: Handles video encoding with configurable quality settings (`-crf 30 -b:v 100k` for balanced quality/speed)
- **FFProbe**: Video metadata extraction with proper error handling
- **IUrlPublisher**: Generic interface for output publishing with `FileSystemUrlPublisher` implementation
- **TestLogger**: Bridges xUnit `ITestOutputHelper` with `ILogger` for consistent logging

### Central Package Management
All package versions are managed in `Directory.Packages.props`. Project files reference packages without version attributes.

### Test Output Location
Test-generated media files are saved to `{CurrentDirectory}/out/debug/` and logged with `file://wsl$/Ubuntu` URLs for direct access in WSL environments.

## Implementation Notes

### Pipe Configuration
- **Default pipe thresholds**: `pauseWriterThreshold: 1024, resumeWriterThreshold: 512` for responsive backpressure
- **BufferBlock capacity**: Limited to 2 frames to trigger backpressure quickly in tests
- **Frame processing**: Uses `ReadOnlySequence<byte>` with proper segmented data handling

### Performance Characteristics
- **Memory efficient**: No large buffer accumulation, frames processed as they arrive
- **Responsive**: Sub-millisecond backpressure response (vs. 500ms polling in previous implementations)
- **Scalable**: Natural flow control prevents memory bloat regardless of video size
- **Deterministic**: Stops at exact buffer limits, verified by automated tests