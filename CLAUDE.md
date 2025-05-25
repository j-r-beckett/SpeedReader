# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

OpusFlow is a .NET 8 video processing solution with three main components:
- **Engine**: Core video processing library using FFMpegCore and SixLabors.ImageSharp
- **Engine.Runner**: Console application entry point
- **Engine.Test**: Test project with video generation and processing tests

## Common Commands

### Build and Test
```bash
# Build entire solution
dotnet build

# Run all tests  
dotnet test

# Run specific test with detailed output
dotnet test --logger "console;verbosity=detailed" --filter "CanRoundTripRedBlueFrames"

# Run single test project
dotnet test Src/Engine.Test/
```

### Development
```bash
# Restore packages
dotnet restore

# Clean build artifacts
dotnet clean
```

## Architecture Notes

### Video Processing Pipeline
The core video processing uses a channel-based architecture:
1. **Frame Generation**: Images are created using SixLabors.ImageSharp and fed into `Channel<Image<Rgb24>>`
2. **Frame Streaming**: `FrameWriter.FramesToStream()` converts image frames to raw RGB24 byte streams via System.IO.Pipelines
3. **Video Encoding**: FFMpegCore processes raw frames through a pipe to generate compressed video (WebM/VP8)

**Important**: FFmpeg runs as a separate process and does not respond to backpressure. The frame streaming must complete before FFmpeg finishes processing, or the pipeline may hang.

### Key Components
- **FrameReader**: Extracts video frames using FFmpeg with backpressure control via `SwitchableStreamPipeSource`
- **FrameWriter**: Handles video encoding with configurable quality settings (`-crf 10 -b:v 1M` for balanced quality/speed)
- **FFProbe**: Video metadata extraction with proper error handling
- **MediaSaver**: Utility for saving test outputs to `out/debug/` with WSL file URLs for easy access
- **TestLogger**: Bridges xUnit `ITestOutputHelper` with `ILogger` for consistent logging

### Central Package Management
All package versions are managed in `Directory.Packages.props`. Project files reference packages without version attributes.

### Test Output Location
Test-generated media files are saved to `{CurrentDirectory}/out/debug/` and logged with `file://wsl$/Ubuntu` URLs for direct access in WSL environments.