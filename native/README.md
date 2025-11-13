# SpeedReader Native ONNX Runtime Integration

This directory contains the work for statically linking ONNX Runtime into SpeedReader's Native AOT binary.

## Documentation

- **[spec.md](spec.md)** - High-level specification and implementation approach
- **[api-notes.md](api-notes.md)** - ONNX Runtime C API usage guide with examples
- **[build-research.md](build-research.md)** - ONNX Runtime static build configuration and requirements

## Goal

Replace the dynamically-linked ONNX Runtime dependency with a statically-linked version to produce a standalone, single-binary distribution of SpeedReader for Linux x64 (CPU-only).

## Current Status

**Planning Phase** - Specifications and research complete, ready for implementation.

## Quick Reference

### ONNX Runtime C API Header
`~/library/onnxruntime/include/onnxruntime/core/session/onnxruntime_c_api.h`

### Build ONNX Runtime Static Library
```bash
cd ~/library/onnxruntime
./build.sh --config Release --parallel --skip_tests
```
Output: `build/Linux/Release/libonnxruntime.a` (+ component libraries)

### Key Technologies
- **C** - Wrapper language (not C++)
- **Zig** - Compiler for C wrapper
- **Docker** - Reproducible build environment
- **.NET Native AOT** - Static library integration via P/Invoke

## Next Steps

1. Create Dockerfile for ONNX Runtime build
2. Write C wrapper implementation
3. Create smoke test
4. Integrate with C# codebase
5. Test and validate
