# SpeedReader Native ONNX Runtime Integration

Statically links ONNX Runtime into SpeedReader's Native AOT binary for Linux x64 (CPU-only).

## Build Process

All builds happen inside Docker for reproducibility. The Dockerfile:
1. Builds ONNX Runtime 1.15.0 as static libraries (~45-60 min first run, cached afterward)
2. Compiles the C wrapper (`speedreader_ort.c`)

### Build Static Libraries

For Native AOT integration:

```bash
./build_static_lib.sh
```

Output:
- `build/speedreader_ort.o` - Compiled wrapper object file
- `build/onnx/` - 55 ONNX Runtime static libraries

### Build Shared Library

For running tests (tests use dynamic linking):

```bash
./build_shared_lib.sh
```

Output: `build/libspeedreader_ort.so` (~24MB)

Requires `./build_static_lib.sh` to run first.

## Files

- **speedreader_ort.{h,c}** - C wrapper around ONNX Runtime C API
- **build_static_lib.sh** - Builds Docker image, extracts individual `.a` libraries and wrapper `.o`
- **build_shared_lib.sh** - Builds `.so` for testing from existing artifacts
- **Dockerfile** - Multi-stage build: ONNX Runtime → wrapper → artifacts
- **onnxruntime_c_api.h** - ONNX Runtime C API header (for reference)
- **onnxruntime_ep_c_api.h** - ONNX Runtime execution provider API header

## CI Integration

The Dockerized build makes CI trivial:

```yaml
- name: Build Native Libraries
  run: cd native && ./build_static_lib.sh && ./build_shared_lib.sh
```

Docker layer caching dramatically speeds up subsequent builds.

## Platform Support

Currently: Linux x64 only

Future: Windows (via Docker + MinGW cross-compile) and macOS (via cross-compilation)
