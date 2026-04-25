// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

#ifndef SPEEDREADER_H
#define SPEEDREADER_H

#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

// ************
// Constants
// ************

#define SPEEDREADER_ERROR_BUF_SIZE 256

// ************
// Opaque handle types
// ************

typedef int64_t SpeedReaderInstance;

// Handle returned by speedreader_submit, used to retrieve the result.
// - Each handle must be passed to exactly one call of speedreader_await or speedreader_cancel.
// - After that call, the handle is invalid and must not be reused.
// - Handles that are neither awaited nor cancelled are leaked.
typedef int64_t SpeedReaderHandle;

// ************
// Status codes
// ************

typedef enum {
    SPEEDREADER_OK = 0,
    SPEEDREADER_ERROR = 1,
    SPEEDREADER_TIMEOUT = 2,  // Only speedreader_await will ever timeout
} SpeedReaderStatus;

// ************
// API
// ************
// - Error buffer must be at least SPEEDREADER_ERROR_BUF_SIZE bytes
// - On error, error buffer contains a null-terminated message
// - Caller may pass NULL for error if not interested in error details

// Instance lifecycle.
// Not thread-safe.
SpeedReaderStatus speedreader_create(
    SpeedReaderInstance* instance,
    char* error
);
void speedreader_destroy(SpeedReaderInstance instance);

// Submit an encoded image (PNG, JPEG, etc.) for OCR.
// Returns a handle for retrieving the result.
// May block under load until the pipeline has capacity (backpressure).
// Thread-safe.
SpeedReaderStatus speedreader_submit(
    SpeedReaderInstance instance,
    const uint8_t* image_data,
    size_t image_len,
    SpeedReaderHandle* handle,
    char* error
);

// Retrieve the result of a previously submitted image.
// timeout_ms < 0:  block until complete
// timeout_ms == 0: non-blocking poll
// timeout_ms > 0:  block up to timeout_ms milliseconds
//
// On success, result_json points to a UTF-8 null-terminated JSON string
// allocated by the library. Caller must free with speedreader_free_result().
// On SPEEDREADER_TIMEOUT, result_json and result_len are not modified.
// Consumes the handle regardless of outcome (except timeout).
// Thread-safe.
SpeedReaderStatus speedreader_await(
    SpeedReaderInstance instance,
    SpeedReaderHandle handle,
    int32_t timeout_ms,
    const uint8_t** result_json,
    size_t* result_len,
    char* error
);

// Cancel a submitted image. Consumes the handle.
// Best-effort: inference already in progress may complete.
// Thread-safe.
SpeedReaderStatus speedreader_cancel(
    SpeedReaderInstance instance,
    SpeedReaderHandle handle
);

// Free a result buffer returned by speedreader_await.
void speedreader_free_result(const uint8_t* result_json);

#ifdef __cplusplus
}
#endif

#endif // SPEEDREADER_H
