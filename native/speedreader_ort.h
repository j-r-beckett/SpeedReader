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
// Opaque handle types
// ************

typedef struct SpeedReaderOrtEnv SpeedReaderOrtEnv;
typedef struct SpeedReaderOrtSession SpeedReaderOrtSession;

// ************
// Response status codes
// ************

typedef enum {
    SPEEDREADER_ORT_OK = 0,
    SPEEDREADER_ORT_UNKNOWN = 1,
    SPEEDREADER_ORT_INVALID_ARGUMENT = 2,
    SPEEDREADER_ORT_TRUNCATED = 3,
} SpeedReaderOrtStatus;

// ************
// Generic output buffer types
// ************
// - Caller allocates buffer, sets capacity in elements
// - Callee writes to buffer, sets length in elements
// - If insufficient buffer capacity, callee returns TRUNCATED
// - Callee does not set TRUNCATED if error buffer is truncated
//   when writing an error message to avoid overwriting the actual error
// - Buffer pointer may only be null when capacity == 0

typedef struct {
    char* buffer;
    size_t capacity;
    size_t length;
} SpeedReaderOrtStringBuf;

typedef struct {
    float* buffer;
    size_t capacity;
    size_t length;
} SpeedReaderOrtFloatBuf;

typedef struct {
    int64_t* buffer;
    size_t capacity;
    size_t length;
} SpeedReaderOrtInt64Buf;

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
// - If caller does not want detailed error information, they may pass a NULL error ptr
// - Error will only have length > 0 if result is not OK

// Environment management (one per process).
// Not thread-safe.
SpeedReaderOrtStatus speedreader_ort_create_env(
    SpeedReaderOrtEnv** env,
    SpeedReaderOrtStringBuf* error
);
void speedreader_ort_destroy_env(SpeedReaderOrtEnv* env);

// Session management (typically one per (model, configuration)).
// Not thread-safe.
SpeedReaderOrtStatus speedreader_ort_create_session(
    SpeedReaderOrtEnv* env,
    const void* model_data,
    size_t model_data_length,
    const SpeedReaderOrtSessionOptions* options,
    SpeedReaderOrtSession** session,
    SpeedReaderOrtStringBuf* error
);
void speedreader_ort_destroy_session(SpeedReaderOrtSession* session);

// Inference execution.
// Thread-safe.
SpeedReaderOrtStatus speedreader_ort_run(
    SpeedReaderOrtSession* session,
    const float* input_data,
    const int64_t* input_shape,
    size_t input_shape_length,
    SpeedReaderOrtFloatBuf* output_data,
    SpeedReaderOrtInt64Buf* output_shape,
    SpeedReaderOrtStringBuf* error
);

#ifdef __cplusplus
}
#endif

#endif // SPEEDREADER_ORT_H
