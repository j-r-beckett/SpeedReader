# CLAUDE.md

# Introduction

SpeedReader is a high-performance OCR engine implemented in C# and compiled to native code for distribution.

# Codebase Map

.
|-- .github
|   |-- workflows
|       `-- build.yml  // Build a statically linked binary in a musl environment
|-- models  // Model files in onnx format
|-- native  // C code and header file for speedreader_ort
|-- Src
|   |-- Frontend  // SpeedReader binary
|   |   |-- Cli
|   |   |-- Server  // Webserver started by ./speedreader serve
|   |   |-- Web  // Example telemetry stack for server mode, useful for local development
|   |       |-- Monitoring  // Prometheus, grafana, otel-collector configuration
|   |       |-- Dockerfile  // Dockerfile for speedreader
|   |       |-- compose.yml  // Speedreader, monitoring infrastructure, Caddy
|   |       `-- run.sh  // Builds speedreader, builds docker image, runs `docker compose down`, runs `docker compose up`
|   |   |-- Program.cs  // Application entrypoint
|   |-- Native  // C# interface to libonnxruntime. Use P/Invokes to call speedreader_ort, which in turn wraps the onnx runtime
|   |-- Ocr  // Core library, contains all Ocr functionality
|   |   |-- Algorithms  // Various algorithms used in detection or recognition
|   |   |-- Geometry  // Geometry pipeline for turning collections of points into OCR bounding boxes
|   |   |-- InferenceEngine  // Abstraction around onnx inference; parallelism, batching, monitoring, adaptive tuning
|   |   |   |-- ServiceCollectionExtensions.cs  // DI for inference engine, used by OcrPipeline DI
|   |   |-- SmartMetrics  // High-resolution OTEL gauges to track averages, throughputs
|   |   |-- Telemetry  // Legacy, to be deleted
|   |   |-- Visualization  // Visualize OCR results as interactive SVGs
|   |   |-- OcrPipeline.cs  // Orchestrates TextDetector, TextRecognizer
|   |   |-- ServiceCollectionExtensions.cs  // DI for OcrPipeline
|   |   |-- TextDetector.cs  // Preprocess -> inference engine (DbNet) -> postprocess
|       `-- TextRecognizer.cs  // Preprocess -> inference engine (SVTRv2) -> postprocess
|   |-- Ocr.Test
|   |   |-- Algorithms
|   |   |-- E2E  // E2E tests exercise inference; debug detection failures by inspecting the generated visualizations (paths logged to console)
|   |   |   |-- TextDetectorE2ETests.cs
|   |   |   |-- OcrPipelineE2ETests.cs
|   |   |   `-- TextRecognizerE2ETests.cs  // NEVER adjust expected confidence; lower than expected confidence ALWAYS indicates a bug
|   |   |-- FlowControl
|       `-- Geometry
|   |-- Resources  // Non-code resources embedded in the TEXT section of the speedreader binary
|   |   |-- CharDict  // Dictionary for text recognition model
|   |   |-- Font  // Avoid dependence on system fonts
|   |   |-- Viz  // SVG template for visualization
|   |   `-- Weights  // Model weights (int8 DbNet, fp32 SVTRv2); symlinks to models/
|   |-- Resources.Test
|   |-- TestUtils
|   |   |-- FileSystemUrlPublisher.cs  // Print filenames as clickable URLs
|       `-- TestLogger.cs  // Log to the console during a unit test; `dotnet test ... --logger "console;verbosity=normal"`
|-- tools
|   |-- build.py  // Build onnx, build speedreader_ort
|   |-- build_dbnet.py  // Convert externally sourced dbnet .pth to onnx, and quantize to int8
|   |-- build_onnx.py  // Build the onnx runtime
|   `-- build_speedreader_libs.py  // Build speedreader_ort
|-- .editorconfig  // Formatting rules
|-- Directory.Packages.props  // Package versions
`-- hello.png  // A test image
