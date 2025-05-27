# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

OpusFlow is a .NET 8 video processing solution with five main components:
- **Engine**: Core video processing library using FFMpegCore, CliWrap, and SixLabors.ImageSharp
- **Engine.Test**: Test project with video generation and processing tests
- **Engine.Benchmark**: Performance benchmarking with Chart.js visualization
- **Models**: ONNX machine learning model management and inference using Microsoft.ML.OnnxRuntime
- **Models.Test**: Test project that verifies the Models build process correctly generated the models

## Common Commands

### Build and Test
```bash
# Build entire solution
dotnet build

# Run all tests with timeout protection (ALWAYS use timeout to prevent hanging)
timeout 30s dotnet test

# Run specific test with detailed output
timeout 30s dotnet test --logger "console;verbosity=detailed" --filter "CanDecodeRedBlueFrames"

# Run backpressure test
timeout 30s dotnet test --logger "console;verbosity=detailed" --filter "BackpressureStopsInputConsumption"

# Run single test project
timeout 30s dotnet test Src/Engine.Test/

# Run Models tests (verifies build process generated valid models)
timeout 30s dotnet test Src/Models.Test/

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

## Models Project Architecture

### Model Build Process
The Models project uses a Docker-based build system to generate ONNX models:
- **buildModels.sh**: Builds a Docker container and extracts generated models to the `models/` directory
- **MSBuild Integration**: Models are automatically built before compilation via `BuildModels` target
- **Auto-copy**: Generated models are included as `None` items and copied to output directory
- **Clean Integration**: `CleanModels` target removes generated models during clean operations

### ModelZoo Class
- **GetInferenceSession()**: Factory method for creating ONNX inference sessions from model enums
- **Model Enum**: Defines available models (DbNet18, Robust31) with corresponding directory names
- **Assembly-relative Paths**: Models are located relative to assembly location for deployment flexibility
- **Standard Structure**: Each model follows `models/{model_name}/end2end.onnx` pattern

### Models.Test Verification
The test suite verifies that the model build process works correctly:
- **CanRetrieveSession**: Parameterized test ensuring all models in the enum can be loaded as valid ONNX sessions
- **Build Verification**: Tests fail if the Docker build process didn't generate valid ONNX files
- **Error Handling**: Validates proper exceptions for missing or invalid model files
- **Enum Coverage**: Uses `MemberData` to automatically test all models defined in the Model enum

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
- **ModelZoo**: ONNX model management with enum-based model selection and InferenceSession factory methods

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

## Reference Index

### Using the Reference Index

**Purpose**: The index is a research starting point, not an answer key. Use it to quickly identify relevant areas when you need deeper technical understanding.

**When to use the index**:
- Implementing new functionality that requires unfamiliar APIs or patterns
- Troubleshooting complex technical issues that may have known solutions
- Understanding best practices for specific technologies (ONNX, ImageSharp, TPL Dataflow, etc.)
- Exploring integration patterns between different systems

**When NOT to use the index**:
- For basic questions easily answered from existing OpusFlow code
- For general programming concepts not specific to the reference libraries
- When the task is straightforward and doesn't require deep technical research

**How to use effectively**:
1. **Start specific**: Look for targeted guides (.md files) before diving into source code
2. **Check for docs**: Look for `/doc/`, `/docs/`, or `README.md` files in relevant repos - they often contain crucial implementation guidance
3. **Be strategic**: Don't try to read everything - identify the most relevant 2-3 areas
4. **Follow the trail**: Use source code examples to understand implementation patterns
5. **Apply immediately**: Use findings to inform your current task, don't just collect information

### Maintaining the Reference Index

**When to update the index**:
- You discover valuable resources in the references that aren't currently indexed
- Existing index entries are incomplete, misdirected, or broken
- New functionality areas emerge that need their own index category
- You find useful cross-connections between different reference areas

**When NOT to update**:
- Every time you use the references (don't over-maintain)
- For minor organizational preferences or style changes
- When uncertain about the long-term value of a resource

**How to update effectively**:
1. **Follow the pattern**: Brief description + path, pointing to information rather than explaining it
2. **Organize thoughtfully**: Use existing categories when possible, create new ones only for clear clusters
3. **Keep it scannable**: Maintain the library catalog metaphor - easy to browse and find relevant sections
4. **Test the addition**: Ask yourself "Would this help someone find useful information faster?"

### Available References

## TPL Dataflow & Streaming
- **./.claude/references/CreatingDataflowBlocks.md** - Microsoft's guide to custom dataflow block implementation
- **./.claude/references/dotnetruntime/src/libraries/System.Threading.Tasks.Dataflow/src/Blocks/ActionBlock.cs** - Reference implementation of async action processing with backpressure
- **./.claude/references/dotnetruntime/src/libraries/System.Threading.Tasks.Dataflow/src/Blocks/BufferBlock.cs** - Core buffering and capacity management patterns
- **./.claude/references/dotnetruntime/src/libraries/System.Threading.Tasks.Dataflow/src/Blocks/TransformBlock.cs** - Synchronous and async transform patterns
- **./.claude/references/dotnetruntime/src/libraries/System.Threading.Tasks.Dataflow/src/Internal/TargetCore.cs** - Target-side message handling and completion coordination
- **./.claude/references/dotnetruntime/src/libraries/System.IO.Pipelines/src/System/IO/Pipelines/Pipe.cs** - Core pipe implementation with backpressure handling
- **./.claude/references/dotnetruntime/src/libraries/System.IO.Pipelines/src/System/IO/Pipelines/PipeWriter.cs** - Writer-side flow control and memory management
- **./.claude/references/dotnetruntime/src/libraries/System.IO.Pipelines/tests/BackpressureTests.cs** - Critical test patterns for flow control verification

## ONNX Machine Learning
- **./.claude/references/dotnetONNXTutorial.md** - C# ONNX Runtime usage guide
- **./.claude/references/onnxruntime/csharp/src/Microsoft.ML.OnnxRuntime/InferenceSession.shared.cs** - Session lifecycle and model loading patterns
- **./.claude/references/onnxruntime/csharp/src/Microsoft.ML.OnnxRuntime/OrtValue.shared.cs** - Memory-efficient tensor handling and native buffer management
- **./.claude/references/onnxruntime/csharp/test/Microsoft.ML.OnnxRuntime.Tests.Common/InferenceTest.cs** - Real-world inference patterns and error handling
- **./.claude/references/mmdeploy/** - Model deployment and optimization tools

## Image Processing
- **./.claude/references/ImageSharp/src/ImageSharp/Image{TPixel}.cs** - Core image manipulation and pixel access patterns
- **./.claude/references/ImageSharp/src/ImageSharp/Memory/MemoryAllocatorExtensions.cs** - Memory-efficient image processing patterns
- **./.claude/references/ImageSharp/src/ImageSharp/PixelFormats/PixelOperations{TPixel}.cs** - High-performance pixel manipulation and conversion patterns

## Polygon Operations
- **./.claude/references/Clipper2Notes.md** - Polygon clipping and offsetting library overview
- **./.claude/references/Clipper2/CSharp/Clipper2Lib/Clipper.Engine.cs** - Core clipping algorithm implementation
- **./.claude/references/Clipper2/CSharp/Clipper2Lib/Clipper.Offset.cs** - Polygon offsetting (dilation/erosion) algorithms essential for DBNet

## Process Management
- **./.claude/references/CliWrap/** - External process execution and piping

## Text Detection (DBNet)
- **./.claude/references/DBNetNotes.md** - Complete C# implementation guide for DBNet inference
- **./.claude/references/DBNetPaper.md** - Original research paper details
- **./.claude/references/mmdeploy/csrc/mmdeploy/codebase/mmocr/cpu/dbnet.cpp** - C++ post-processing implementation with contour detection and polygon scoring

## Video Processing
- **./.claude/references/FFmpeg/fftools/ffmpeg_filter.c** - Filter graph management and frame processing coordination

## .NET Platform
- **./.claude/references/dotnetruntime/** - Core .NET runtime implementation
- **./.claude/references/dotnetdocs/** - Official .NET documentation
- **./.claude/references/dotnet-api-docs/** - API reference documentation
- **./.claude/references/AspNetCore.Docs/** - ASP.NET Core documentation

### Knowledge Base
<!-- Claude: Record specific files and insights you discover for future reference -->
