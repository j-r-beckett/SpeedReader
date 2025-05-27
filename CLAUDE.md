# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

OpusFlow is a .NET 8 video processing solution with five main components:
- **Engine**: Core video processing library using FFMpegCore, CliWrap, and SixLabors.ImageSharp
- **Engine.Test**: Test project with video generation and processing tests
- **Engine.Benchmark**: Performance benchmarking with Chart.js visualization
- **Models**: ONNX machine learning model management and inference using Microsoft.ML.OnnxRuntime
- **Models.Test**: Test project that verifies the Models build process correctly generated the models

## Common Commands

### Build and Test
```bash
# Build entire solution
dotnet build

# Run all tests with timeout protection (ALWAYS use timeout to prevent hanging)
timeout 30s dotnet test

# Run specific test with detailed output
timeout 30s dotnet test --logger "console;verbosity=detailed" --filter "CanDecodeRedBlueFrames"

# Run backpressure test
timeout 30s dotnet test --logger "console;verbosity=detailed" --filter "BackpressureStopsInputConsumption"

# Run single test project
timeout 30s dotnet test Src/Engine.Test/

# Run Models tests (verifies build process generated valid models)
timeout 30s dotnet test Src/Models.Test/

# Clean and restore
dotnet clean
dotnet restore
```

## Implementation Notes

### Project Management
- When creating new projects, adding projects to the solution, and adding or removing packages, make changes using the dotnet cli

## Architecture Notes

# Backpressure Implementation in OpusFlow

## Core Principle
**FFmpeg + Unix pipes provide natural backpressure coordination** - when stdout blocks, FFmpeg automatically pauses stdin. No manual throttling needed.

[... rest of the existing content remains unchanged ...]