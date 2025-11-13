# ONNX Runtime Static Build Research

## Summary

ONNX Runtime **supports static library builds** via CMake configuration. The default is to build a shared library, but static linking is explicitly supported and used in production (e.g., mobile deployments).

## Build Configuration

### CMake Option
**Source:** `~/library/onnxruntime/cmake/CMakeLists.txt:111`

```cmake
option(onnxruntime_BUILD_SHARED_LIB "Build a shared library" OFF)
```

**Default:** OFF (static library)
**To build shared:** `-Donnxruntime_BUILD_SHARED_LIB=ON`
**To build static:** Just omit the flag (or explicitly `-Donnxruntime_BUILD_SHARED_LIB=OFF`)

### Build Script Usage

The `build.sh` wrapper script accepts `--build_shared_lib` flag:

```bash
# Static library (default)
./build.sh --config Release --parallel

# Shared library
./build.sh --config Release --parallel --build_shared_lib
```

The script forwards to `tools/ci_build/build.py`, which configures CMake accordingly.

## Build Commands for SpeedReader

### CPU-Only Static Library

```bash
cd ~/library/onnxruntime
./build.sh \
  --config Release \
  --parallel \
  --skip_tests \
  --build_dir build/cpu-static
```

This produces: `build/cpu-static/Linux/Release/libonnxruntime.a`

### Minimal Build (Reduce Size)

```bash
./build.sh \
  --config MinSizeRel \
  --parallel \
  --skip_tests \
  --disable_contrib_ops \
  --disable_ml_ops \
  --build_dir build/cpu-static-minimal
```

`MinSizeRel` configuration optimizes for binary size over performance.

### With Additional Flags

```bash
./build.sh \
  --config Release \
  --parallel \
  --skip_tests \
  --cmake_extra_defines CMAKE_POSITION_INDEPENDENT_CODE=ON
```

`CMAKE_POSITION_INDEPENDENT_CODE=ON` may be needed for static linking into shared libraries.

## Build Output Structure

After building:
```
~/library/onnxruntime/
└── build/
    └── Linux/  (or MacOS, depending on platform)
        └── Release/
            ├── libonnxruntime.a          # Static library
            ├── libonnxruntime_common.a   # Internal component
            ├── libonnxruntime_framework.a
            ├── libonnxruntime_graph.a
            ├── libonnxruntime_mlas.a
            ├── libonnxruntime_optimizer.a
            ├── libonnxruntime_providers.a
            ├── libonnxruntime_session.a
            ├── libonnxruntime_util.a
            └── external/
                └── protobuf/
                    └── libprotobuf-lite.a  # Statically linked dependency
```

**Key Detail:** ONNX Runtime is built as multiple static libraries that must all be linked together. The main `libonnxruntime.a` depends on the internal component libraries.

## Linking Requirements

### Linux Static Link Command

```bash
zig cc \
  wrapper.c \
  libonnxruntime.a \
  libonnxruntime_common.a \
  libonnxruntime_framework.a \
  libonnxruntime_graph.a \
  libonnxruntime_mlas.a \
  libonnxruntime_optimizer.a \
  libonnxruntime_providers.a \
  libonnxruntime_session.a \
  libonnxruntime_util.a \
  external/protobuf/libprotobuf-lite.a \
  -lstdc++ \
  -lpthread \
  -lm \
  -o libspeedreader_ort.a
```

**Order matters:** Dependencies must be listed in reverse dependency order (dependents before dependencies).

### Required System Libraries

- `libstdc++` - C++ standard library (ONNX Runtime is C++)
- `libpthread` - Threading support
- `libm` - Math library
- `libdl` - Dynamic loading (even for static builds, used internally)

## Size Estimates

Based on similar projects and ONNX Runtime architecture:

- **libonnxruntime.a (all components):** ~80-120MB (uncompressed)
- **With protobuf and dependencies:** ~100-150MB total
- **Final linked binary (with SpeedReader):** ~150-200MB

Size can be reduced with `MinSizeRel` config and stripping symbols.

## Known Considerations

### 1. Protobuf Static Linking

ONNX Runtime statically links Protobuf. On Windows, ensure `protobuf_BUILD_SHARED_LIBS=OFF`. On Linux, ensure PIC (Position Independent Code) is enabled if needed.

### 2. MSVC Runtime (Windows Only)

For Windows builds with static VC runtime:
```bash
--cmake_extra_defines CMAKE_MSVC_RUNTIME_LIBRARY="MultiThreaded"
```

### 3. Mobile/Minimal Builds

ONNX Runtime has extensive build options for minimal builds (used in mobile):
- `--minimal_build` - Removes unused operators
- `--disable_exceptions` - Removes C++ exception support
- `--disable_rtti` - Removes runtime type information

These are **not recommended** for SpeedReader (may break compatibility), but good to know they exist.

## Verification Commands

After building, verify the static library:

```bash
# Check if it's a static library
file libonnxruntime.a
# Output: libonnxruntime.a: current ar archive

# List symbols
nm -C libonnxruntime.a | grep OrtGetApiBase
# Should show OrtGetApiBase symbol

# Check size
ls -lh libonnxruntime.a
```

## Docker Build Strategy

Since ONNX Runtime builds take 30-60 minutes, Docker caching is critical:

```dockerfile
# Stage 1: Build ONNX Runtime (rarely changes)
FROM ubuntu:22.04 AS onnx-builder
RUN apt-get update && apt-get install -y \
    build-essential cmake ninja-build python3 git
COPY onnxruntime/ /src/onnxruntime/
WORKDIR /src/onnxruntime
RUN ./build.sh --config Release --parallel --skip_tests
# Cache this layer - only rebuild when ONNX Runtime version changes

# Stage 2: Build C wrapper (changes frequently)
FROM onnx-builder AS wrapper-builder
COPY wrapper.c /src/
RUN zig cc wrapper.c /src/onnxruntime/build/Linux/Release/*.a -o libspeedreader_ort.a
```

## References

- CMake option: `~/library/onnxruntime/cmake/CMakeLists.txt:111`
- Build script: `~/library/onnxruntime/build.sh`
- Build driver: `~/library/onnxruntime/tools/ci_build/build.py`
- Static library linking notes: `~/library/onnxruntime/docs/cmake_guideline.md`

## Action Items

1. ✅ **Confirmed:** Static library builds are fully supported
2. **Next:** Write Dockerfile to build ONNX Runtime static library
3. **Next:** Test build with actual source (estimate 45-60 min first build)
4. **Next:** Document exact `.a` files needed for linking
