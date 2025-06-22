# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SpeedReader is a .NET 10.0 OCR (Optical Character Recognition) solution with seven main components:
- **Core**: CLI application for text detection and recognition in images and videos
- **Video**: Video processing library using FFMpegCore, CliWrap, and streaming components
- **Video.Test**: Test project with video generation and processing tests
- **Models**: ONNX machine learning model management using Microsoft.ML.OnnxRuntime
- **Models.Test**: Test project that verifies the Models build process correctly generated the models
- **Ocr**: OCR functionality with DBNet text detection and SVTRv2 text recognition
- **Ocr.Test**: Test project for OCR components and end-to-end text processing

## Common Commands

### Build and Test
```bash
# Build entire solution
dotnet build

# Run all tests with timeout protection (ALWAYS use timeout to prevent hanging)
# IMPORTANT: Always build first to prevent stale tests when build fails
timeout 60s bash -c "dotnet build && dotnet test --no-build"

# Run specific test with detailed output
timeout 60s bash -c "dotnet build && dotnet test --no-build --logger 'console;verbosity=detailed' --filter 'CanDecodeRedBlueFrames'"

# Run backpressure test
timeout 60s bash -c "dotnet build && dotnet test --no-build --logger 'console;verbosity=detailed' --filter 'BackpressureStopsInputConsumption'"

# Run single test project
timeout 60s bash -c "dotnet build && dotnet test Src/Video.Test/ --no-build"

# Run Models tests (verifies build process generated valid models)
timeout 60s bash -c "dotnet build && dotnet test Src/Models.Test/ --no-build"

# Run OCR tests (end-to-end text detection and recognition)
timeout 60s bash -c "dotnet build && dotnet test Src/Ocr.Test/ --no-build"

# Clean and restore
dotnet clean
dotnet restore

# Run the CLI application
dotnet run --project Src/Core/ -- [arguments]
```

## Architecture Notes

### OCR Pipeline
SpeedReader processes images through a complete OCR pipeline:

1. **Input**: Image or video frame (via Core CLI application)
2. **Text Detection**: DBNet model identifies text regions with bounding boxes
3. **Text Recognition**: SVTRv2 model reads text content from detected regions
4. **Output**: Annotated image with bounding boxes and recognized text

### Backpressure Implementation

**FFmpeg + Unix pipes provide natural backpressure coordination** - when stdout blocks, FFmpeg automatically pauses stdin. No manual throttling needed.

#### Decoder Chain
`Stream` → `StreamingPipeSource` → `FFmpeg` → `StreamingPipeTarget` → `BufferBlock<Image<Rgb24>>(capacity=2)` → Consumer

**Slow consumer** → BufferBlock fills → `SendAsync()` blocks → PipeReader stops → StreamingPipeTarget pipe fills → **Unix pipe blocks** → FFmpeg stdout blocks → **FFmpeg pauses stdin** → input stream stops

#### Encoder Chain
Producer → `ActionBlock<Image<Rgb24>>(capacity=2)` → `Pipe.Writer` → `StreamingPipeSource` → `FFmpeg` → `StreamingPipeTarget` → Consumer

**Key insight**: Unix pipes + FFmpeg = automatic coordination. Don't reinvent this with polling or manual throttling.

### Models Project Architecture

#### Model Build Process
The Models project uses a Docker-based build system to generate ONNX models:
- **buildModels.sh**: Builds a Docker container and extracts generated models to the `models/` directory
- **MSBuild Integration**: Models are automatically built before compilation via `BuildModels` target
- **Auto-copy**: Generated models are included as `None` items and copied to output directory
- **Clean Integration**: `CleanModels` target removes generated models during clean operations

#### ModelZoo Class
- **GetInferenceSession()**: Factory method for creating ONNX inference sessions from model enums
- **Model Enum**: Defines available models (DbNet18, SVTRv2) with corresponding directory names
- **Assembly-relative Paths**: Models are located relative to assembly location for deployment flexibility
- **Standard Structure**: Each model follows `models/{model_name}/end2end.onnx` pattern

#### Models and Models.Test: Stale File Prevention

**Models Project:**
- **Generated files**: `models/` directory contains Docker-generated ONNX models and metadata
- **Incremental builds**: Uses `models/.built` marker file - only rebuilds when `modelBuilder/` files change
- **Clean behavior**: `dotnet clean` removes `models/` directory and `.built` marker

**Models.Test Project:**
- **Clean generated models**: `CleanModelsBeforeBuild` target removes copied models directory before every build
- **Canary function**: Tests validate models load correctly, detecting stale model issues in other projects

### OCR Components

#### Text Detection (DBNet)
- **Input**: RGB images in NCHW format (N, C=3, H, W)
- **Output**: Probability maps NHW (N, H, W) indicating text detection confidence
- **Post-processing**: Contour detection, polygon generation, and filtering in `Ocr/Algorithms/`

#### Text Recognition (SVTRv2)
- **Input**: Text region images normalized to height 48, variable width
- **Output**: CTC character sequence probabilities
- **Vocabulary**: 6625 characters (multilingual Chinese+English)
- **Decoding**: CTC decoding to extract final text strings

#### Streaming Architecture
- **TPL Dataflow**: Bounded blocks propagate backpressure to application layer
- **System.IO.Pipelines**: Memory-efficient streaming with configurable thresholds
- **Concurrent Processing**: Frame processing happens concurrently with FFmpeg processing using `Task.WhenAll()`

### Core Application
- **CLI Interface**: Command-line tool for processing images and videos
- **Self-contained**: Single-file deployment with embedded FFmpeg binaries
- **Output Publishing**: Flexible output system with file system publisher

### Implementation Notes

#### Central Package Management
All package versions are managed in `Directory.Packages.props`. Project files reference packages without version attributes.

#### Test Output Location
Test-generated media files are saved to `/tmp/` and logged with `file://wsl$/Ubuntu` URLs for direct access in WSL environments.

#### Performance Characteristics
- **Memory efficient**: No large buffer accumulation, frames processed as they arrive
- **Responsive**: Sub-millisecond backpressure response via Unix pipe coordination
- **Scalable**: Natural flow control prevents memory bloat regardless of input size
- **Deterministic**: Stops at exact buffer limits, verified by automated tests