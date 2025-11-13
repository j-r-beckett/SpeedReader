# SpeedReader Native ONNX Runtime Integration

## Objective

Replace the dynamically-linked ONNX Runtime dependency with a statically-linked version to produce standalone, single-binary distributions of SpeedReader.

## Background

Currently, SpeedReader uses the `Microsoft.ML.OnnxRuntime.Gpu` NuGet package, which provides C# bindings that dynamically load native ONNX Runtime libraries (`.so`, `.dll`, `.dylib`) at runtime. This requires shipping these libraries alongside the binary or expecting users to have them installed.

The goal is to statically link ONNX Runtime into the Native AOT-compiled binary, eliminating all runtime dependencies except system libraries.

## Scope

### In Scope
- Building ONNX Runtime as a static library (CPU-only)
- Creating a minimal C API wrapper around ONNX Runtime
- Integrating the static library with .NET Native AOT compilation
- Linux x64 target platform
- Dockerized, reproducible build environment

### Out of Scope
- GPU/CUDA support (future work)
- Multi-platform support (Windows/macOS - future work)
- Modifications to ONNX Runtime itself
- Custom ONNX operators or execution providers
- TensorRT support

## Architecture

### Current Implementation

`Src/Ocr/InferenceEngine/OnnxInferenceKernel.cs` currently:
1. Creates an `InferenceSession` with `SessionOptions` (thread counts, profiling)
2. Loads model weights via `ModelLoader`
3. Executes inference via `InferenceSession.Run()` using `OrtValue` tensors
4. Returns output tensor data as `float[]` and shape as `int[]`

Multiple inference sessions run in parallel, controlled by `ManagedExecutor` with a semaphore-based parallelism limit.

### Proposed Implementation

Replace dynamic ONNX Runtime with static linking:
1. Build ONNX Runtime as a static library (`.a` on Linux, `.lib` on Windows)
2. Create a thin C wrapper exposing session creation, inference execution, and cleanup
3. Use P/Invoke from C# to call the C wrapper
4. Link the static library into the Native AOT binary at compile time

### API Surface

The ONNX Runtime C API provides all necessary functionality:
- Session creation from model bytes
- Configuration of threading and execution options
- Running inference with input/output tensors
- Memory management and cleanup

The C wrapper should expose a minimal API matching SpeedReader's needs:
- Create session (from byte array, with thread configuration)
- Run inference (float array + shape → float array + shape)
- Destroy session

## Technology Choices

### C (not C++)
- Simpler ABI for P/Invoke integration
- ONNX Runtime provides a stable C API (`onnxruntime_c_api.h`)
- No C++ runtime linking complexity

### Zig Compiler
- Trivial cross-compilation without toolchain setup
- Consistent behavior across platforms
- Modern C compiler with better error messages
- Docker-friendly

### Docker for Build Reproducibility
- Multi-stage builds for ONNX Runtime compilation
- Cacheable layers (ONNX Runtime build is time-consuming)
- Consistent environment across developers and CI/CD
- Eliminates "works on my machine" issues

## Target Build

### Linux x64 CPU-Only
- Target: Users without GPU, edge devices, development/testing, general CLI usage
- Binary size: ~120-150MB
- Dependencies: Only glibc, libm (standard Linux system libraries)
- ONNX Runtime built without CUDA support

## Implementation Approach

1. Build ONNX Runtime static library (CPU-only)
2. Write C wrapper using ONNX Runtime C API
3. Create standalone C smoke test to validate wrapper with actual model
4. Compile wrapper with Zig, link with ONNX Runtime static lib
5. Verify smoke test passes before C# integration
6. Create P/Invoke bindings in C#
7. Replace `OnnxInferenceKernel` implementation
8. Integrate static library with Native AOT build
9. Test inference, verify results match current implementation
10. Validate binary has no ONNX Runtime dynamic dependencies

**Success Criteria:**
- Smoke test successfully runs inference with one of SpeedReader's models
- `ldd speedread` shows only libc/libm/ld-linux (no libonnxruntime.so)
- OCR inference produces identical results to current implementation
- Single self-contained binary runs on Ubuntu 22.04+ without additional installation

## Future Work

After completing the Linux x64 CPU-only build, two natural extensions:

1. **GPU Support**: Build ONNX Runtime with CUDA execution provider, dynamically linking system CUDA/cuDNN libraries
2. **Multi-Platform**: Leverage Zig's cross-compilation for Windows x64, macOS x64, macOS ARM64

## Integration Points

### .NET Native AOT
The Native AOT compiler supports statically linking native libraries via project configuration. The C wrapper will be referenced as a native library during the publish step.

P/Invoke with `[DllImport("__Internal")]` is used to reference statically-linked symbols.

### Existing Codebase
All other dependencies (Clipper2, ImageSharp, Npgsql, Fluid.Core, etc.) are pure managed .NET assemblies that compile directly into the Native AOT binary. ONNX Runtime is the only native dependency requiring static linking.

The inference engine abstraction (`IInferenceKernel` interface) allows the new native implementation to coexist with or replace the existing managed implementation without affecting the rest of the OCR pipeline.

## Non-Goals

This effort does not aim to:
- Improve inference performance (current implementation is already optimized)
- Change the OCR algorithm or model architecture
- Support additional ONNX Runtime execution providers beyond CPU
- Create a general-purpose ONNX Runtime C# binding (only what SpeedReader needs)

## Open Questions

None at this time. Implementation details will be discovered during development.

## References

- [ONNX Runtime C API Documentation](https://onnxruntime.ai/docs/api/c/)
- [ONNX Runtime Build Instructions](https://onnxruntime.ai/docs/build/)
- [.NET Native AOT Interop](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [Zig C Compiler](https://ziglang.org/)
