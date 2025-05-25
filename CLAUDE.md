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

# Backpressure Implementation in OpusFlow

## Core Principle
**FFmpeg + Unix pipes provide natural backpressure coordination** - when stdout blocks, FFmpeg automatically pauses stdin. No manual throttling needed.

## Backpressure Chains

### Decoder
`Stream` → `StreamingPipeSource` → `FFmpeg` → `StreamingPipeTarget` → `BufferBlock<Image<Rgb24>>(capacity=2)` → Consumer

**Slow consumer** → BufferBlock fills → `SendAsync()` blocks → PipeReader stops → StreamingPipeTarget pipe fills → **Unix pipe blocks** → FFmpeg stdout blocks → **FFmpeg pauses stdin** → input stream stops

### Encoder  
Producer → `ActionBlock<Image<Rgb24>>(capacity=2)` → `Pipe.Writer` → `StreamingPipeSource` → `FFmpeg` → `StreamingPipeTarget` → Consumer

**Slow consumer** → StreamingPipeTarget pipe fills → **Unix pipe blocks** → FFmpeg stdout blocks → **FFmpeg pauses stdin** → `FlushAsync()` blocks → ActionBlock fills → `SendAsync()` blocks → producer pauses

## Architecture Components
- **Unix pipes**: OS-level blocking provides instant backpressure signal
- **FFmpeg coordination**: Process automatically pauses stdin when stdout blocks
- **TPL Dataflow**: Bounded blocks propagate backpressure to application layer
- **System.IO.Pipelines**: Memory-efficient streaming with configurable thresholds, responds to backpressure

## Completion Coordination
- **Decoder**: `await Task.WhenAll(ffmpegTask, frameProcessingTask)`
- **Encoder**: Wait for `targetBlock.Completion` then `inputPipe.Writer.CompleteAsync()`

## Verification
`BackpressureStopsInputConsumption` test: Large stream stops at exactly 131,072 bytes with no consumer, proving perfect flow control.

**Key insight**: Unix pipes + FFmpeg = automatic coordination. Don't reinvent this with polling or manual throttling.

### Streaming Architecture Components

- **StreamingPipeSource**: Custom `PipeSource` implementation that feeds video data to FFmpeg stdin with natural backpressure
- **StreamingPipeTarget**: Custom `PipeTarget` implementation that receives FFmpeg stdout via `System.IO.Pipelines.Pipe` with configurable buffer thresholds
- **FfmpegDecoderBlockCreator**: Creates `ISourceBlock<Image<Rgb24>>` that streams decoded frames with bounded capacity for backpressure
- **FfmpegEncoderBlockCreator**: Creates `ITargetBlock<Image<Rgb24>>` that accepts frames and produces streaming encoded video output
- **Concurrent Processing**: Frame processing happens concurrently with FFmpeg processing using `Task.WhenAll()`

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

## Reference Materials

### Source Code Consultation
**DIRECTIVE**: These repositories are authoritative sources for API behavior and implementation details. Consult them when additional context would be helpful for completing a task effectively.

**Available References:**
- **System.IO.Pipelines**: `/home/jimmy/.claude/repos/runtime/src/libraries/System.IO.Pipelines/`
- **CliWrap**: `/home/jimmy/.claude/repos/CliWrap/CliWrap/`
- **ImageSharp**: `/home/jimmy/.claude/repos/ImageSharp/src/ImageSharp/`
- **FFmpeg Documentation**: `/home/jimmy/.claude/repos/FFmpeg/doc/`
- **System.Threading.Tasks.Dataflow**: `/home/jimmy/.claude/repos/runtime/src/libraries/System.Threading.Tasks.Dataflow/`
- **CliWrap Tests**: `/home/jimmy/.claude/repos/CliWrap/CliWrap.Tests/`

### Knowledge Base
<!-- Claude: Record specific files and insights you discover for future reference -->