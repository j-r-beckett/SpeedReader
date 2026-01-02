// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

#include "speedreader_cpuinfo.h"
#include <cpuinfo.h>
#include <stdlib.h>
#include <string.h>
#include <stdio.h>

// ************
// Internal structures
// ************

typedef struct {
    int32_t linux_id;
    uint64_t frequency;
} CpuCandidate;

// ************
// Error handling helpers
// ************

static void write_error(char* error, const char* msg) {
    if (error == NULL || msg == NULL) {
        return;
    }
    size_t msg_len = strlen(msg);
    size_t copy_len = msg_len < SPEEDREADER_CPUINFO_ERROR_BUF_SIZE - 1
                    ? msg_len
                    : SPEEDREADER_CPUINFO_ERROR_BUF_SIZE - 1;
    memcpy(error, msg, copy_len);
    error[copy_len] = '\0';
}

static void clear_error(char* error) {
    if (error != NULL) {
        error[0] = '\0';
    }
}

// ************
// Comparison function for sorting by frequency (descending)
// ************

static int compare_by_frequency_desc(const void* a, const void* b) {
    const CpuCandidate* ca = (const CpuCandidate*)a;
    const CpuCandidate* cb = (const CpuCandidate*)b;

    // Sort by frequency descending (higher frequency first = P-cores before E-cores)
    if (cb->frequency > ca->frequency) return 1;
    if (cb->frequency < ca->frequency) return -1;
    return 0;
}

// ************
// API implementation
// ************

SpeedReaderCpuInfoStatus speedreader_cpuinfo_get_optimal_cpus(
    SpeedReaderOptimalCpus* result,
    char* error
) {
    clear_error(error);

    if (result == NULL) {
        write_error(error, "result parameter is NULL");
        return SPEEDREADER_CPUINFO_ERROR;
    }

    // Initialize result
    result->count = 0;

    // Initialize cpuinfo (safe to call multiple times)
    if (!cpuinfo_initialize()) {
        write_error(error, "cpuinfo_initialize failed");
        return SPEEDREADER_CPUINFO_ERROR;
    }

    uint32_t l2_count = cpuinfo_get_l2_caches_count();
    if (l2_count == 0) {
        write_error(error, "no L2 caches found");
        return SPEEDREADER_CPUINFO_ERROR;
    }

    // Collect one CPU per L2 cache (primary thread only)
    CpuCandidate candidates[SPEEDREADER_CPUINFO_MAX_CPUS];
    int32_t candidate_count = 0;

    for (uint32_t i = 0; i < l2_count && candidate_count < SPEEDREADER_CPUINFO_MAX_CPUS; i++) {
        const struct cpuinfo_cache* l2 = cpuinfo_get_l2_cache(i);
        if (l2 == NULL) {
            continue;
        }

        // Find the first physical thread (smt_id == 0) using this L2
        for (uint32_t p = 0; p < l2->processor_count; p++) {
            const struct cpuinfo_processor* proc = cpuinfo_get_processor(l2->processor_start + p);
            if (proc == NULL) {
                continue;
            }

            // Only pick primary threads (never hyperthreads)
            if (proc->smt_id == 0) {
                candidates[candidate_count].linux_id = proc->linux_id;
                candidates[candidate_count].frequency = proc->core != NULL ? proc->core->frequency : 0;
                candidate_count++;
                break;  // One per L2
            }
        }
    }

    if (candidate_count == 0) {
        write_error(error, "no suitable CPUs found");
        return SPEEDREADER_CPUINFO_ERROR;
    }

    // Sort by frequency descending (P-cores first, then E-cores)
    qsort(candidates, candidate_count, sizeof(CpuCandidate), compare_by_frequency_desc);

    // Copy to result
    for (int32_t i = 0; i < candidate_count; i++) {
        result->cpu_indices[i] = candidates[i].linux_id;
    }
    result->count = candidate_count;

    return SPEEDREADER_CPUINFO_OK;
}
