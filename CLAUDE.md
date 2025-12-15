# CLAUDE.md

# Introduction

SpeedReader is a high-performance OCR engine implemented in C# and compiled to native code for distribution.

# Codebase Map

.
|-- .editorconfig  // Generic formatting rules (indent, newlines)
|-- .external  // External repos cloned on demand by build scripts (gitignored)
|-- .github
|   `-- workflows  // Workflow stubs that call composite actions in ci/actions/
|       |-- static.yml  // Stub for static build
|       `-- dynamic.yml  // Stub for dynamic build
|-- ci  // CI composite actions and local development tooling
|   |-- actions
|   |   |-- static-build  // Build statically linked binary in Alpine/musl environment
|   |   |   `-- action.yml
|   |   `-- dynamic-build  // Build dynamically linked binary on Ubuntu
|   |       `-- action.yml
|   `-- act.py  // Run workflows locally with act; handles container reuse, cleanup, artifacts
|-- models  // Model files in onnx format
|   |-- analyze_onnx.py  // Analyze onnx model: metadata, tensors, parameters, operators; --netron to visualize
|   |-- build_dbnet.py  // Convert dbnet .pth to onnx via mmdeploy, quantize to int8
|   `-- build_svtr.py  // Build SVTRv2 from OpenOCR source
|-- src  // All C# source code and build configuration
|   |-- .editorconfig  // C#-specific formatting and style rules
|   |-- Directory.Build.props  // MSBuild properties
|   |-- Directory.Build.rsp  // MSBuild response file
|   |-- Directory.Packages.props  // Central package management
|   |-- global.json  // .NET SDK version
|   |-- SpeedReader.slnx  // Solution file
|   |-- Benchmarks  // BenchmarkDotNet performance benchmarks
|   |   `-- DryPipelineBenchmark.cs  // Pipeline benchmarks (preprocess, postprocess)
|   |-- Frontend  // SpeedReader binary
|   |   |-- Cli
|   |   |-- Server  // Webserver started by ./speedreader serve
|   |   |-- Web  // Example telemetry stack for server mode, useful for local development
|   |   |   |-- Monitoring  // Prometheus, grafana, otel-collector configuration
|   |   |   |-- Dockerfile  // Dockerfile for speedreader
|   |   |   |-- compose.yml  // Speedreader, monitoring infrastructure, Caddy
|   |   |   `-- run.sh  // Builds speedreader, builds docker image, runs `docker compose down`, runs `docker compose up`
|   |   `-- Program.cs  // Application entrypoint
|   |-- Frontend.Test  // Frontend integration tests
|   |   |-- ApiE2ETests.cs
|   |   `-- WebSocketTests.cs
|   |-- Native  // C# interface to libonnxruntime. Use P/Invokes to call speedreader_ort, which in turn wraps the onnx runtime
|   |   |-- onnx  // ONNX runtime build infrastructure
|   |   |   `-- build.py  // Build the onnx runtime
|   |   `-- speedreader_ort  // C wrapper for ONNX runtime
|   |       |-- build.py  // Build speedreader_ort
|   |       |-- speedreader_ort.c
|   |       `-- speedreader_ort.h
|   |-- Native.Test  // Native library tests
|   |-- Ocr  // Core library, contains all Ocr functionality
|   |   |-- Algorithms  // Various algorithms used in detection or recognition
|   |   |-- Geometry  // Geometry pipeline for turning collections of points into OCR bounding boxes
|   |   |-- InferenceEngine  // Abstraction around onnx inference; parallelism, batching, monitoring, adaptive tuning
|   |   |   `-- ServiceCollectionExtensions.cs  // DI for inference engine, used by OcrPipeline DI
|   |   |-- SmartMetrics  // High-resolution OTEL gauges to track averages, throughputs
|   |   |-- Telemetry  // Legacy, to be deleted
|   |   |-- Visualization  // Visualize OCR results as interactive SVGs
|   |   |-- OcrPipeline.cs  // Orchestrates TextDetector, TextRecognizer
|   |   |-- ServiceCollectionExtensions.cs  // DI for OcrPipeline
|   |   |-- TextDetector.cs  // Preprocess -> inference engine (DbNet) -> postprocess
|   |   `-- TextRecognizer.cs  // Preprocess -> inference engine (SVTRv2) -> postprocess
|   |-- Ocr.Test
|   |   |-- Algorithms
|   |   |-- E2E  // E2E tests exercise inference; debug detection failures by inspecting the generated visualizations (paths logged to console)
|   |   |   |-- TextDetectorE2ETests.cs
|   |   |   |-- OcrPipelineE2ETests.cs
|   |   |   `-- TextRecognizerE2ETests.cs  // NEVER adjust expected confidence; lower than expected confidence ALWAYS indicates a bug
|   |   |-- FlowControl
|   |   `-- Geometry
|   |-- Resources  // Non-code resources embedded in the TEXT section of the speedreader binary
|   |   |-- CharDict  // Dictionary for text recognition model
|   |   |-- Font  // Avoid dependence on system fonts
|   |   |-- Viz  // SVG template for visualization
|   |   `-- Weights  // Model weights (int8 DbNet, fp32 SVTRv2); symlinks to models/
|   |-- Resources.Test
|   `-- TestUtils
|       |-- FileSystemUrlPublisher.cs  // Print filenames as clickable URLs
|       `-- TestLogger.cs  // Log to the console during a unit test; `dotnet test ... --logger "console;verbosity=normal"`
|-- build_utils  // Shared utilities package for build scripts
|   |-- __init__.py  // Public API exports
|   `-- utils.py  // bash(), info(), error(), ensure_repo(), etc.
|-- pre-commit.py  // Git pre-commit hook script
`-- hello.png  // A test image

# Languages

## C#

- Always use `dotnet package` commands for adding, removing, updating packages. Avoid writing XML manually
- There is an excellent set of unit tests only `dotnet test` away. Use it
- Don't use XML style comments
- *Always* build and test in two separate commands. `dotnet` will run tests even if the build fails, which can cover up errors. Always `dotnet build && dotnet test ...`

## Python

- *Always* use uv: `uv run myscript.py`
- All automation should be written in Python instead of shell scripts. This is to ensure cross-platform portability
- An annotated python example is below. All python code should use these patterns:

```python
#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "build_utils"]
#
# [tool.uv.sources]
# build_utils = { path = "RELATIVE_PATH/build_utils", editable = true }
# ///

# Always use uv inline dependencies and a uv shebang

from pathlib import Path
import click
from build_utils import bash, info, error, ScriptError, ensure_repo

SCRIPT_DIR = Path(__file__).parent.resolve()


@click.command()  # Always use Click. Always make --help informative
def main():
    # Use ensure_repo() to clone/checkout git repos. Takes a destination path, git url, and tag
    ensure_repo("out_dir/my_dependency", "https://github.com/my/dependency", "v0.1.0")

    # Use bash() to run shell commands. *Always* shift complexity out of bash and into python.
    # bash() should be used for invoking external tools or commands with no python equivalent.
    # Everything else (including interpreting the results of shell commands) should be done in python
    bash("gcc ...", directory=SCRIPT_DIR)

    # Use info() for status messages. Don't go overboard, only emit status message for significant milestones in the script
    info("successfully build my_dependency")


if __name__ == "__main__":
    # Always use this pattern for calling main()
    try:
        main()
    except ScriptError as e:  # Raise a ScriptError anywhere in the script to crash if something goes wrong. Fail fast!
        error(f"Fatal: {e}")
        exit(1)
```

## C

- Prefer to push complexity out of unsafe C and into managed C#

# Build

SpeedReader can be built as either a managed (CLR) or unmanaged (Native AOT) executable. The managed build is for development, the unmanaged build is for distribution. The native executable can be statically or dynamically linked, for a total of three build 'flavors': managed, static, dynamic.

| Flavor  | speedreader_ort Link | Onnx Runtime Link | Commands                                    |
| ------- | -------------------- | ----------------- | ------------------------------------------- |
| managed | Dynamic              | Dynamic           | `dotnet build`, `dotnet run`, `dotnet test` |
| dynamic | Static               | Dynamic           | `dotnet publish -p:OnnxLinkMode=Dynamic`    |
| static  | Static               | Static            | `dotnet publish -p:OnnxLinkMode=Static`     |

- The dynamic flavor **statically** links speedreader_ort
- The dynamic flavor is for users who want to use an onnx runtime build with hwaccel support
- The static flavor also statically links system libs. To achieve this, it must be built on a musl system. `uv run ci/act.py static` will do this


The build is orchestrated by msbuild. All integrations with native libraries are handled by the Native project. Native, and by extension any project that references Native, exposes these options:

| Option       | Values | Description                              |
| ------------ | ------ | ---------------------------------------- |
| BuildOnnx    | 'true' | Triggers a new onnx build if 'true'      |
| BuildSROrt   | 'true' | Triggers a new speedreader_ort if 'true' |

The Frontend project (SpeedReader executable, the Native AOT target) exposes these options when publishing:

| Options      | Values              | Description                           |
| ------------ | ------------------- | ------------------------------------- |
| OnnxLinkMode | 'Static', 'Dynamic' | Onnx runtime link mode                |
| BuildMusl    | 'true'              | Statically link system libs if `true` |

Examples:

```bash
dotnet publish src/Frontend -r linux-x64  -p:OnnxLinkMode=Dynamic  # build unamanaged dynamically linked executable using cached onnx runtime and speedreader_ort artifacts
dotnet build src -p:BuildSROrt=true  # a managed build with a fresh build of speedreader_ort and cached onnx runtime artifacts
```

SpeedReader uses vertical builds. That means we run the entire build from scratch on each supported platform, without trying to reuse or cache dependencies across platforms.

The entire build is automated using python. Having python as an intermediate layer between msbuild and the shell lets us write complex, flexible, cross-platform build logic.

We use `act` to run our ci pipelines locally. Useful for when you want to build in a clean, containerized environment. Only way to build a fully static executable (uses an Alpine container). See `ci/act.py`.

# Models



# Library

When you need to understand external dependencies (CLI tools, libraries, frameworks, obscure stdlib APIs), **clone the source repo and grep it** instead of web searching. Source code is authoritative; web results are secondhand.

- Read .claude/supplementary/library.md before investigating any external dependency
- Use this approach FIRST, before web search
