// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

#ifndef SPEEDREADER_CPUINFO_H
#define SPEEDREADER_CPUINFO_H

#ifndef __linux__
#error "speedreader_cpuinfo is Linux-only"
#endif

#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

// ************
// Constants
// ************

#define SPEEDREADER_CPUINFO_MAX_CPUS 256
#define SPEEDREADER_CPUINFO_ERROR_BUF_SIZE 256

// ************
// Response status codes
// ************

typedef enum {
    SPEEDREADER_CPUINFO_OK = 0,
    SPEEDREADER_CPUINFO_ERROR = 1,
} SpeedReaderCpuInfoStatus;

// ************
// Result structure
// ************

typedef struct {
    int32_t cpu_indices[SPEEDREADER_CPUINFO_MAX_CPUS];
    int32_t count;
} SpeedReaderOptimalCpus;

// ************
// API
// ************
// - Error buffer must be at least SPEEDREADER_CPUINFO_ERROR_BUF_SIZE bytes
// - On error, error buffer contains null-terminated message
// - On success, error[0] is set to '\0'
// - Caller may pass NULL for error if not interested in error details

// Get optimal CPU indices for affinitized inference threads.
//
// Algorithm: One thread per L2 cache, primary thread only (smt_id == 0).
// Returns P-cores first (sorted by frequency descending), then E-cores.
// cpu_indices contains Linux CPU IDs suitable for pthread_setaffinity_np.
//
// Thread-safe after first call (cpuinfo_initialize is idempotent).
SpeedReaderCpuInfoStatus speedreader_cpuinfo_get_optimal_cpus(
    SpeedReaderOptimalCpus* result,
    char* error
);

#ifdef __cplusplus
}
#endif

#endif // SPEEDREADER_CPUINFO_H
