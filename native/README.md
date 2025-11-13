# SpeedReader Native ONNX Runtime Integration

This directory contains the work for statically linking ONNX Runtime into SpeedReader's Native AOT binary.

## Documentation

- **[spec.md](spec.md)** - High-level specification and implementation approach
- **[api-notes.md](api-notes.md)** - ONNX Runtime C API usage guide with examples
- **[build-research.md](build-research.md)** - ONNX Runtime static build configuration and requirements

## Goal

Replace the dynamically-linked ONNX Runtime dependency with a statically-linked version to produce a standalone, single-binary distribution of SpeedReader for Linux x64 (CPU-only).

## Current Status

**C Wrapper Complete** - ONNX Runtime built, C API wrapper implemented and tested. Ready for C# integration.

## Build Instructions

### 1. Build ONNX Runtime Static Libraries (one-time setup)
```bash
./build.sh
```
This uses Docker to build ONNX Runtime 1.15.0 as static libraries (~45-60 min first run).
Output: `build/onnx/*.a` files (~55 libraries)

### 2. Compile C Wrapper and Create Combined Library
```bash
./compile.sh
```
Output:
- `build/libspeedreader_ort.a` (80MB) - Combined static library for C# integration
- `build/smoke_test` (21MB) - Standalone test binary

### 3. Run Smoke Test
```bash
./build/smoke_test <path_to_svtr_model.onnx>
```
Example:
```bash
./build/smoke_test ../Src/Resources/models/svtrv2_base_ctc/end2end.onnx
```

## Files

- **speedreader_ort.h** - C API header for P/Invoke
- **speedreader_ort.c** - C wrapper implementation
- **smoke_test.c** - Standalone test that validates the wrapper
- **compile.sh** - Builds wrapper object, smoke test, and combined static library
- **build.sh** - Docker-based ONNX Runtime build
- **Dockerfile** - ONNX Runtime build environment

## Key Technologies
- **C** - Wrapper language (stable ABI for P/Invoke)
- **Docker** - Reproducible ONNX Runtime build environment
- **.NET Native AOT** - Static library integration via P/Invoke

## Next Steps

1. ✅ ~~Create Dockerfile for ONNX Runtime build~~
2. ✅ ~~Write C wrapper implementation~~
3. ✅ ~~Create smoke test~~
4. ✅ ~~Build combined static library~~
5. Create P/Invoke bindings in C#
6. Integrate with existing inference engine
7. Test and validate
