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
|   |-- act.py  // Run workflows locally with act; handles container reuse, cleanup, artifacts
|   `-- coverage.py  // Run tests with coverage collection; generates HTML report
|-- models  // Model files in onnx format
|   |-- analyze_onnx.py  // Get info about an onnx model
|   |-- build_dbnet.py  // Convert dbnet .pth to onnx via mmdeploy, quantize to int8
|   |-- build_svtr.py  // Build SVTRv2 from OpenOCR source
|   |-- dbnet_resnet18_fpnc_1200e_icdar2015_fp32.onnx  // All model files (.onnx) are stored in git lfs
|   |-- dbnet_resnet18_fpnc_1200e_icdar2015_int8.onnx
|   `-- svtrv2_base_ctc_fp32.onnx
|-- src  // All C# source code and build configuration
|   |-- .editorconfig  // C#-specific formatting and style rules
|   |-- Directory.Build.props  // MSBuild properties; managed *and unmanaged* dependency versions
|   |-- Directory.Build.rsp  // MSBuild response file
|   |-- Directory.Packages.props  // Central package management
|   |-- global.json  // .NET SDK version
|   |-- SpeedReader.slnx  // Solution file
|   |-- MicroBenchmarks  // BenchmarkDotNet performance benchmarks
|   |   |-- DryPipelineBenchmark.cs  // Pipeline benchmarks (preprocess, postprocess)
|   |   `-- Cli
|   |       `-- InferenceBenchmark.cs  // CLI tool for raw inference benchmarks; outputs to stdout for notebook analysis
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
|   |-- Native  // C# interface to native libraries. Use P/Invokes to call C wrappers
|   |   |-- Onnx  // ONNX runtime inference (InferenceSession, OrtValue, SessionOptions)
|   |   |   |-- Internal  // P/Invoke bindings (SpeedReaderOrt, SafeHandles, Environment)
|   |   |   |-- onnx  // ONNX runtime build infrastructure
|   |   |   |   `-- build.py  // Build the onnx runtime
|   |   |   `-- speedreader_ort  // C wrapper for ONNX runtime
|   |   |       |-- build.py  // Build speedreader_ort
|   |   |       |-- speedreader_ort.c
|   |   |       `-- speedreader_ort.h
|   |   `-- CpuInfo  // CPU topology detection (CpuTopology, CpuInfoException)
|   |       |-- Internal  // P/Invoke bindings (SpeedReaderCpuInfo)
|   |       `-- speedreader_cpuinfo  // C wrapper for cpuinfo
|   |           |-- build.py  // Build speedreader_cpuinfo
|   |           |-- speedreader_cpuinfo.c
|   |           `-- speedreader_cpuinfo.h
|   |-- Native.Test  // Native library tests
|   |   |-- Onnx  // InferenceSession, OrtValue tests
|   |   `-- CpuInfo  // CpuTopology tests
|   |-- Ocr  // Core library, contains all Ocr functionality
|   |   |-- Algorithms  // Various algorithms used in detection or recognition
|   |   |-- Geometry  // Geometry pipeline for turning collections of points into OCR bounding boxes
|   |   |-- InferenceEngine  // Abstraction around onnx inference; parallelism, batching, monitoring, adaptive tuning
|   |   |   `-- ServiceCollectionExtensions.cs  // DI for inference engine, used by OcrPipeline DI
|   |   |-- SmartMetrics  // High-resolution OTEL gauges to track averages, throughputs
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
|-- notebooks  // Marimo notebooks for data analysis and visualization
|   |-- helpers.py  // Shared utilities for notebooks
|   `-- dbnet_duration.py  // Analyze DbNet inference duration across threading configs
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

| Option         | Values | Description                                  |
| -------------- | ------ | -------------------------------------------- |
| BuildOnnx      | 'true' | Triggers a new onnx build if 'true'          |
| BuildSROrt     | 'true' | Triggers a new speedreader_ort if 'true'     |
| BuildSRCpuInfo | 'true' | Triggers a new speedreader_cpuinfo if 'true' |
| DeepClean      | 'true' | Clean all native build artifacts             |

The Frontend project (SpeedReader executable, the Native AOT target) exposes these options when publishing:

| Options      | Values              | Description                           |
| ------------ | ------------------- | ------------------------------------- |
| OnnxLinkMode | 'Static', 'Dynamic' | Onnx runtime link mode                |
| BuildMusl    | 'true'              | Statically link system libs if `true` |

Examples:

```bash
dotnet publish src/Frontend -r linux-x64  -p:OnnxLinkMode=Dynamic  # build unamanaged dynamically linked executable using cached onnx runtime and speedreader_ort artifacts
dotnet build src -p:BuildSROrt=true  # managed build with a fresh build of speedreader_ort and cached onnx runtime artifacts
```

SpeedReader uses vertical builds. That means we run the entire build from scratch on each supported platform, without trying to reuse or cache dependencies across platforms.

The entire build is automated using python. Having python as an intermediate layer between msbuild and the shell lets us write complex, flexible, cross-platform build logic.

We use `act` to run our ci pipelines locally. Useful for when you want to build in a clean, containerized environment. Only way to build a fully static executable (uses an Alpine container). See `ci/act.py`.

# Models

SpeedReader uses open-source pre-trained models.

`models/analyze_onnx.py` can get info about a model (parameter count, input/output tensors, operators, etc) or visualize it for the user using netron.

We currently use models with dynamic dimensions, but in code we use fixed dimensions of 640x640 for detection and 160x48 for recognition. 

When running SpeedReader with the CPU inference provider performance is overwhelmingly dominated by DbNet inference, which in turn is bottlenecked by memory bandwidth. SVTRv2 inference by contrast is extremely fast. The difference is to differences in the sizes of the inputs causing cache <-> RAM thrashing--large inputs increase the amount of memory needed by each stage of the model, and once stages get large enough to not fit into cache you get thrashing, which kills performance. DbNet input tensors are (batch_size == 1) is 640x640x3 = 1228800 values, while SVTRv2 input tensors are 160x48x3 = 23040, so DbNet inference thrashes like crazy while SVTRv2 inferences fits comfortably in cache.

Model: DbNet
Quantizations: fp32, int8
Input Tensor:
    - float32 [batch, 3, height, width]
    - A batch of images in CHW format
Output Tensor:
    - float32 [batch, width, height]
    - A batch of probability maps (1 => 'text near this pixel')
    - DbNet identifies individual words, each word (also known as a fragment) is a connected component in the probability map
    - The probability maps do not directly represent bounding boxes. Generally speaking, DbNet draws a narrow rectangle in the center of each detected word
    - Use the `--viz` flag to generate a visualization of the probability maps and bounding boxes
Author: https://github.com/open-mmlab/mmocr/blob/main/configs/textdet/dbnet/README.md

Model: SVTRv2
Quantizations: fp32
Input Tensor: 
    - float32 [batch, 3, 48, width]
    - A batch of images of individual words in CHW format
    - If you run analyze_onnx.py, you'll see 'int_height' in the width position. This is just poor naming, int_height represents width
Output Tensor:
    - float32 [batch, seq_len, dict_size]
    - CTC
    - For our model, seq_len = width/8 and dict_size = 6625
Author: https://github.com/Topdu/OpenOCR/blob/main/configs/rec/svtrv2/readme.md


# Library

When you need to understand external dependencies (CLI tools, libraries, frameworks, obscure stdlib APIs), **clone the source repo and grep it** instead of web searching. Source code is authoritative; web results are secondhand.

- Read .claude/supplementary/library.md before investigating any external dependency
- Use this approach FIRST, before web search
