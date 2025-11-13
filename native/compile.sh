#!/bin/bash
# Copyright (c) 2025 j-r-beckett
# Licensed under the Apache License, Version 2.0

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/build"
ONNX_LIB_DIR="$BUILD_DIR/onnx"

echo "=== Compiling SpeedReader ORT Wrapper ==="
echo ""

# Check if ONNX Runtime libraries exist
if [ ! -d "$ONNX_LIB_DIR" ]; then
    echo "ERROR: ONNX Runtime libraries not found at $ONNX_LIB_DIR"
    echo "Please run ./build.sh first to build ONNX Runtime static libraries"
    exit 1
fi

echo "1. Compiling wrapper library..."

# Compile the wrapper as a static library
gcc -c \
    -O3 \
    -fPIC \
    -I"$SCRIPT_DIR" \
    -o "$BUILD_DIR/speedreader_ort.o" \
    "$SCRIPT_DIR/speedreader_ort.c"

echo "   ✓ Compiled speedreader_ort.o"
echo ""

echo "2. Compiling smoke test..."

# Collect all ONNX Runtime static libraries (order matters for linking)
ONNX_LIBS=(
    # ONNX Runtime core libraries
    "$ONNX_LIB_DIR/libonnxruntime_session.a"
    "$ONNX_LIB_DIR/libonnxruntime_optimizer.a"
    "$ONNX_LIB_DIR/libonnxruntime_providers.a"
    "$ONNX_LIB_DIR/libonnxruntime_util.a"
    "$ONNX_LIB_DIR/libonnxruntime_framework.a"
    "$ONNX_LIB_DIR/libonnxruntime_graph.a"
    "$ONNX_LIB_DIR/libonnxruntime_mlas.a"
    "$ONNX_LIB_DIR/libonnxruntime_common.a"
    "$ONNX_LIB_DIR/libonnxruntime_flatbuffers.a"

    # ONNX proto libraries (order matters - onnx depends on onnx_proto depends on protobuf)
    "$ONNX_LIB_DIR/_deps/onnx-build/libonnx.a"
    "$ONNX_LIB_DIR/_deps/onnx-build/libonnx_proto.a"

    # Protobuf (full version needed for onnx_proto)
    "$ONNX_LIB_DIR/_deps/protobuf-build/libprotobuf.a"

    # Abseil (required by ONNX Runtime)
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/hash/libabsl_hash.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/hash/libabsl_city.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/hash/libabsl_low_level_hash.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/container/libabsl_raw_hash_set.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/container/libabsl_hashtablez_sampler.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/synchronization/libabsl_synchronization.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/synchronization/libabsl_graphcycles_internal.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/strings/libabsl_strings.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/strings/libabsl_strings_internal.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/strings/libabsl_cord.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/strings/libabsl_cord_internal.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/strings/libabsl_cordz_functions.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/strings/libabsl_cordz_handle.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/strings/libabsl_cordz_info.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/base/libabsl_base.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/base/libabsl_spinlock_wait.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/base/libabsl_throw_delegate.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/base/libabsl_raw_logging_internal.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/base/libabsl_log_severity.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/base/libabsl_malloc_internal.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/numeric/libabsl_int128.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/time/libabsl_time.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/time/libabsl_time_zone.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/time/libabsl_civil_time.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/debugging/libabsl_stacktrace.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/debugging/libabsl_symbolize.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/debugging/libabsl_debugging_internal.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/debugging/libabsl_demangle_internal.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/profiling/libabsl_exponential_biased.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/types/libabsl_bad_optional_access.a"
    "$ONNX_LIB_DIR/_deps/abseil_cpp-build/absl/types/libabsl_bad_variant_access.a"

    # nsync (Google's synchronization library)
    "$ONNX_LIB_DIR/_deps/google_nsync-build/libnsync_cpp.a"

    # cpuinfo (CPU detection)
    "$ONNX_LIB_DIR/_deps/pytorch_cpuinfo-build/libcpuinfo.a"
    "$ONNX_LIB_DIR/_deps/pytorch_cpuinfo-build/deps/clog/libclog.a"

    # RE2 (regex library)
    "$ONNX_LIB_DIR/_deps/re2-build/libre2.a"

    # Flatbuffers
    "$ONNX_LIB_DIR/_deps/flatbuffers-build/libflatbuffers.a"
)

# Link smoke test with wrapper and ONNX Runtime libraries
g++ \
    -O3 \
    -I"$SCRIPT_DIR" \
    -o "$BUILD_DIR/smoke_test" \
    "$SCRIPT_DIR/smoke_test.c" \
    "$BUILD_DIR/speedreader_ort.o" \
    "${ONNX_LIBS[@]}" \
    -lpthread \
    -lm \
    -ldl

echo "   ✓ Compiled smoke_test"
echo ""

echo "3. Creating combined static library for C# integration..."

# Create MRI script for combining archives
MRI_SCRIPT="$BUILD_DIR/combine.mri"
cat > "$MRI_SCRIPT" << EOF
CREATE $BUILD_DIR/libspeedreader_ort.a
ADDMOD $BUILD_DIR/speedreader_ort.o
EOF

# Add all ONNX Runtime libraries to the MRI script
for lib in "${ONNX_LIBS[@]}"; do
    echo "ADDLIB $lib" >> "$MRI_SCRIPT"
done

echo "SAVE" >> "$MRI_SCRIPT"
echo "END" >> "$MRI_SCRIPT"

# Execute the MRI script to combine all archives
ar -M < "$MRI_SCRIPT"

# Remove the MRI script
rm "$MRI_SCRIPT"

LIB_SIZE=$(du -h "$BUILD_DIR/libspeedreader_ort.a" | cut -f1)
echo "   ✓ Created libspeedreader_ort.a ($LIB_SIZE)"
echo ""

echo "=== Build Complete ==="
echo "Smoke test: $BUILD_DIR/smoke_test"
echo "Static library: $BUILD_DIR/libspeedreader_ort.a"
echo ""
echo "Run smoke test:"
echo "  $BUILD_DIR/smoke_test <path_to_svtr_model.onnx>"