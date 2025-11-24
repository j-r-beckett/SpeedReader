# CLAUDE.md

## Guidance
- ...
- 100% test pass rate is enforced by a pre-commit hook. The ONLY acceptable pass rate for tests is 100%
- Changing test tuning or disabling tests to get to 100% pass rate is NEVER acceptable
- Feature development is only complete once the build succeeds with zero warnings and all tests pass
- When working on a feature, try to build and test while developing the feature instead of only once at the end
- Avoid excessive or overly helpful commenting
- Do not make any changes outside the scope of the current feature
- If the user mentions a type you're not familiar with, learn how to use it by doing a quick search through the codebase
- Always use synthetic data during testing. Use ImageSharp to generate images with text. See examples in existing tests for how to do this properly
- If the user mentions any URLs, types, or files, always be sure to read them in their entirety even if they don't seem immediately relevant. Do this even if the user doesn't explicitly ask you to view the URL/type/file. You should, on your initiative, access and read EVERY reference mentioned by the user.

## Projects

### Frontend
Application entrypoint. Builds a self-contained binary that can identify and recognize text in images and videos. Can be used either as a CLI tool or as an API server.

### Frontend.Test

### Ocr
OCR functionality. Uses a TPL Dataflow pipeline: text detection (DBNet) -> text recognition (SVTR) -> post-processing.

The entire pipeline is wrapped by Src/Ocr/Blocks/OcrBlock.cs. When trying to understand how the pipeline works, start in OcrBlock and expand outward.

All control flow blocks should have BoundedCapacity = 1. This ensures backpressure, and prevents the pipeline from having excessive internal capacity.

All blocks should propagate completion. This can be done either by using `.LinkTo(new DataflowLinkOptions { PropagateCompletion = true })`, or with this idiom:

```csharp
block.Completion.ContinueWith(t =>
{
    if (t.IsFaulted)
    {
        ((IDataflowBlock)nextBlock).Fault(t.Exception);
    }
    else
    {
        nextBlock.Complete();
    }
});
```

When trying to understand how the parts of Dataflow pipeline fit together, focus on the input and output types of blocks and how blocks are linked together.

Complex algorithms are implemented in static classes in Src/Ocr/Algorithms.

### Ocr.Test

### Resources
Embeds application resources in the application binary and makes them available to other projects.

Contains models, fonts, the SVG visualization template, and ffmpeg and ffprobe binaries. All embedded resources should be managed by this project.

### Resources.Test

### Video
Decodes and encodes videos using ffmpeg.

Starts an ffmpeg process using CliWrap, then interacts with it using pipes to provide backpressure (ffmpeg stops accepting input over stdin when its internal buffer is full).

### Video.Test

### TestUtils
Contains utilities useful for testing.
- Backpressure: utility for verifying that Dataflow blocks emit and respond to backpressure
- CapturingLogger: saves logger output
- TestLogger: log to console in a unit test
- FileSystemUrlPublisher: saves images (and other data) to disk, and writes log messages containing URLs that point to written files

## Development

## Package Management
- All package versions are managed in `Directory.Packages.props`. Project files reference packages without version attributes
- Use `dotnet package add` to add new packages and `dotnet reference add` to add new project references. Do not edit project files manually
- Never introduce a new dependency without consulting the user

## Build and Test
```bash
# Build entire solution
dotnet build

# Run all tests with timeout protection (ALWAYS use timeout to prevent hanging)
# IMPORTANT: Always build first to prevent stale tests when build fails
timeout 60s bash -c "dotnet build && dotnet test --no-build"

# Run specific test with detailed output
timeout 60s bash -c "dotnet build && dotnet test --no-build --logger 'console;verbosity=detailed' --filter 'CanDecodeRedBlueFrames'"

# Run a standalone C# file, useful for experimentation
dotnet run MyFile.cs
```

## CLI
```bash
# Run the CLI application
dotnet run --project Src/Frontend/ -- [arguments]

# Start and wait for debugger to attach
SPEEDREADER_DEBUG_WAIT=true dotnet run --project Src/Frontend/ -- [arguments]
```

```bash
# CLI help
$ dotnet run --project Src/Frontend/ -- -h
Description:
  SpeedReader - Blazing fast OCR

Usage:
  speedread [<inputs>...] [command] [options]

Arguments:
  <inputs>  Input image files

Options:
  --serve                        Run as HTTP server
  --viz <Basic|Diagnostic|None>  Visualization mode [default: None]
  --json                         Full JSON output with detailed metadata and confidence scores
  --version                      Show version information
  -?, -h, --help                 Show help and usage information


Commands:
  video <path> <frameRate>  Process video files with OCR
```
