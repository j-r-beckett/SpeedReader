# Copyright (c) 2025 j-r-beckett
# Licensed under the Apache License, Version 2.0

FROM ubuntu:22.04 AS onnx-builder

# Install build dependencies
RUN apt-get update && apt-get install -y \
    build-essential \
    ninja-build \
    python3 \
    python3-pip \
    git \
    wget \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Install CMake 3.26+ (required by ONNX Runtime 1.15.0)
RUN wget -q https://github.com/Kitware/CMake/releases/download/v3.27.7/cmake-3.27.7-linux-x86_64.sh && \
    chmod +x cmake-3.27.7-linux-x86_64.sh && \
    ./cmake-3.27.7-linux-x86_64.sh --skip-license --prefix=/usr/local && \
    rm cmake-3.27.7-linux-x86_64.sh

# Clone ONNX Runtime 1.15.0
WORKDIR /build
RUN git clone --recursive --depth 1 --branch v1.15.0 \
    https://github.com/microsoft/onnxruntime.git

# Build ONNX Runtime as static library (CPU-only)
WORKDIR /build/onnxruntime
RUN ./build.sh \
    --config Release \
    --parallel \
    --skip_tests \
    --allow_running_as_root \
    --cmake_extra_defines CMAKE_POSITION_INDEPENDENT_CODE=ON

# Stage 2: Build SpeedReader wrapper
FROM onnx-builder AS wrapper-builder

# Copy wrapper source
COPY speedreader_ort.c speedreader_ort.h onnxruntime_c_api.h onnxruntime_ep_c_api.h /build/wrapper/

# Compile wrapper object file
WORKDIR /build/wrapper
RUN gcc -c \
    -O3 \
    -fPIC \
    -I/build/wrapper \
    -o speedreader_ort.o \
    speedreader_ort.c