# CLAUDE.md

# Introduction

SpeedReader is a high-performance OCR engine implemented in C# and compiled to native code for distribution.

# Codebase Map

.
|-- .github
|   |-- workflows  // Thin wrappers that trigger ci/ workflows on push
|       |-- static.yml  // Triggers ci/static.yml
|       `-- dynamic.yml  // Triggers ci/dynamic.yml
|-- ci  // CI workflow definitions and local development tooling
|   |-- static.yml  // Build statically linked binary in Alpine/musl environment
|   |-- dynamic.yml  // Build dynamically linked binary on Ubuntu
|   `-- act.py  // Run workflows locally with act; handles container reuse, cleanup, artifacts
|-- .external  // External repos cloned on demand by build scripts (gitignored)
|-- models  // Model files in onnx format
|   |-- build_dbnet.py  // Convert dbnet .pth to onnx via mmdeploy, quantize to int8
|   |-- build_svtr.py  // Build SVTRv2 from OpenOCR source
|-- Src
|   |-- Frontend  // SpeedReader binary
|   |   |-- Cli
|   |   |-- Server  // Webserver started by ./speedreader serve
|   |   |-- Web  // Example telemetry stack for server mode, useful for local development
|   |   |   |-- Monitoring  // Prometheus, grafana, otel-collector configuration
|   |   |   |-- Dockerfile  // Dockerfile for speedreader
|   |   |   |-- compose.yml  // Speedreader, monitoring infrastructure, Caddy
|   |   |   `-- run.sh  // Builds speedreader, builds docker image, runs `docker compose down`, runs `docker compose up`
|   |   `-- Program.cs  // Application entrypoint
|   |-- Native  // C# interface to libonnxruntime. Use P/Invokes to call speedreader_ort, which in turn wraps the onnx runtime
|   |   |-- onnx  // ONNX runtime build infrastructure
|   |   |   `-- build.py  // Build the onnx runtime
|   |   `-- speedreader_ort  // C wrapper for ONNX runtime
|   |       |-- build.py  // Build speedreader_ort
|   |       |-- speedreader_ort.c
|   |       `-- speedreader_ort.h
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
|-- tools
|   `-- utils  // Shared utilities package for build scripts
|       |-- __init__.py  // Public API exports
|       `-- utils.py  // bash(), info(), error(), ensure_repo(), etc.
|-- .editorconfig  // Formatting rules
|-- Directory.Packages.props  // Package versions
`-- hello.png  // A test image

# Languages

## C#

- Always use `dotnet package` commands for adding, removing, updating packages. Avoid writing XML manually
- There is an excellent set of unit tests only `dotnet test` away. Use it
- Don't use XML style comments

## Python

- *Always* use uv: `uv run myscript.py`
- All automation should be written in Python instead of shell scripts
- Read .claude/supplementary/python.md before doing any python work

## C

- Prefer to push complexity out of unsafe C and into managed C#

# Library

