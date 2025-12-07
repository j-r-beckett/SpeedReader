// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

#ifndef SPEEDREADER_ORT_H
#define SPEEDREADER_ORT_H

#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

// ************
// Constants
// ************

#define SPEEDREADER_ORT_ERROR_BUF_SIZE 256
#define SPEEDREADER_ORT_MAX_SHAPE_DIMS 16

// ************
// Opaque handle types
// ************

typedef struct SpeedReaderOrtEnv SpeedReaderOrtEnv;
typedef struct SpeedReaderOrtSession SpeedReaderOrtSession;

// ************
// Response status codes
// ************

typedef enum {
    SPEEDREADER_ORT_OK = 0,
    SPEEDREADER_ORT_ERROR = 1,
} SpeedReaderOrtStatus;

// ************
// Session options
// ************

typedef struct {
    int32_t intra_op_num_threads;
    int32_t inter_op_num_threads;
    int32_t enable_profiling;  // 0 = disabled, 1 = enabled
} SpeedReaderOrtSessionOptions;

// ************
// API
// ************
// - Error buffer must be at least SPEEDREADER_ORT_ERROR_BUF_SIZE bytes
// - On error, error buffer contains null-terminated message
// - On success, error[0] is set to '\0'
// - Caller may pass NULL for error if not interested in error details

// Environment management (one per process).
// Not thread-safe.
SpeedReaderOrtStatus speedreader_ort_create_env(
    SpeedReaderOrtEnv** env,
    char* error
);
void speedreader_ort_destroy_env(SpeedReaderOrtEnv* env);

// Session management (typically one per (model, configuration)).
// Not thread-safe.
SpeedReaderOrtStatus speedreader_ort_create_session(
    SpeedReaderOrtEnv* env,
    const void* model_data,
    size_t model_data_size,
    const SpeedReaderOrtSessionOptions* options,
    SpeedReaderOrtSession** session,
    char* error
);
void speedreader_ort_destroy_session(SpeedReaderOrtSession* session);

// Inference execution.
// Thread-safe.
//
// - output_data: caller-allocated buffer, must be exactly output_count elements
// - output_count: expected number of float elements (must match actual output)
// - output_shape: caller-allocated buffer, at least SPEEDREADER_ORT_MAX_SHAPE_DIMS elements
// - output_ndim: out parameter, set to actual number of dimensions
//
// Returns error if output_count does not match actual output size.
// Error message includes expected vs actual sizes for debugging.
SpeedReaderOrtStatus speedreader_ort_run(
    SpeedReaderOrtSession* session,
    const float* input_data,
    const int64_t* input_shape,
    size_t input_ndim,
    float* output_data,
    size_t output_count,
    int64_t* output_shape,
    size_t* output_ndim,
    char* error
);

#ifdef __cplusplus
}
#endif

#endif // SPEEDREADER_ORT_H
