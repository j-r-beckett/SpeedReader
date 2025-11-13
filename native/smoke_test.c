// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

#include "speedreader_ort.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>

// SVTR model configuration
#define INPUT_CHANNELS 3
#define INPUT_HEIGHT 48
#define INPUT_WIDTH 160
#define INPUT_SIZE (INPUT_CHANNELS * INPUT_HEIGHT * INPUT_WIDTH)
#define OUTPUT_CLASSES 6625

// Error buffer size
#define ERROR_BUF_SIZE 512

static void print_error(const char* context, SpeedReaderOrtStringBuf* error) {
    if (error->length > 0) {
        fprintf(stderr, "ERROR [%s]: %s\n", context, error->buffer);
    } else {
        fprintf(stderr, "ERROR [%s]: Unknown error (no message)\n", context);
    }
}

static void* load_model(const char* path, size_t* size_out) {
    FILE* f = fopen(path, "rb");
    if (!f) {
        fprintf(stderr, "Failed to open model file: %s\n", path);
        return NULL;
    }

    fseek(f, 0, SEEK_END);
    size_t size = ftell(f);
    fseek(f, 0, SEEK_SET);

    void* data = malloc(size);
    if (!data) {
        fprintf(stderr, "Failed to allocate memory for model\n");
        fclose(f);
        return NULL;
    }

    size_t read = fread(data, 1, size, f);
    fclose(f);

    if (read != size) {
        fprintf(stderr, "Failed to read complete model file\n");
        free(data);
        return NULL;
    }

    *size_out = size;
    return data;
}

int main(int argc, char** argv) {
    if (argc != 2) {
        fprintf(stderr, "Usage: %s <path_to_svtr_model.onnx>\n", argv[0]);
        return 1;
    }

    printf("=== SpeedReader ORT Wrapper Smoke Test ===\n\n");

    // Allocate error buffer
    char error_buf[ERROR_BUF_SIZE];
    SpeedReaderOrtStringBuf error = {
        .buffer = error_buf,
        .capacity = ERROR_BUF_SIZE,
        .length = 0
    };

    SpeedReaderOrtStatus status;
    SpeedReaderOrtEnv* env = NULL;
    SpeedReaderOrtSession* session = NULL;
    void* model_data = NULL;
    float* input_data = NULL;
    float* output_data = NULL;
    int64_t* output_shape = NULL;

    // Step 1: Create environment
    printf("1. Creating ONNX Runtime environment...\n");
    status = speedreader_ort_create_env(&env, &error);
    if (status != SPEEDREADER_ORT_OK) {
        print_error("create_env", &error);
        return 1;
    }
    printf("   ✓ Environment created\n\n");

    // Step 2: Load model
    printf("2. Loading SVTR model from disk...\n");
    size_t model_size;
    model_data = load_model(argv[1], &model_size);
    if (!model_data) {
        speedreader_ort_destroy_env(env);
        return 1;
    }
    printf("   ✓ Loaded %zu bytes\n\n", model_size);

    // Step 3: Create session
    printf("3. Creating inference session...\n");
    SpeedReaderOrtSessionOptions options = {
        .intra_op_num_threads = 4,
        .inter_op_num_threads = 1,
        .enable_profiling = 0
    };

    status = speedreader_ort_create_session(env, model_data, model_size, &options, &session, &error);
    if (status != SPEEDREADER_ORT_OK) {
        print_error("create_session", &error);
        free(model_data);
        speedreader_ort_destroy_env(env);
        return 1;
    }
    printf("   ✓ Session created with %d intra-op threads\n\n", options.intra_op_num_threads);

    // Step 4: Prepare input (all zeros)
    printf("4. Preparing input tensor...\n");
    input_data = (float*)calloc(INPUT_SIZE, sizeof(float));
    if (!input_data) {
        fprintf(stderr, "Failed to allocate input buffer\n");
        speedreader_ort_destroy_session(session);
        free(model_data);
        speedreader_ort_destroy_env(env);
        return 1;
    }

    int64_t input_shape[] = {1, INPUT_CHANNELS, INPUT_HEIGHT, INPUT_WIDTH};
    printf("   ✓ Input shape: [%ld, %ld, %ld, %ld]\n",
           input_shape[0], input_shape[1], input_shape[2], input_shape[3]);
    printf("   ✓ Input data: %d elements (all zeros)\n\n", INPUT_SIZE);

    // Step 5: Allocate output buffers
    printf("5. Allocating output buffers...\n");

    // Output shape buffer (max 4 dimensions should be enough)
    output_shape = (int64_t*)malloc(4 * sizeof(int64_t));
    if (!output_shape) {
        fprintf(stderr, "Failed to allocate output shape buffer\n");
        free(input_data);
        speedreader_ort_destroy_session(session);
        free(model_data);
        speedreader_ort_destroy_env(env);
        return 1;
    }

    // Output data buffer (estimate: timesteps=40, classes=6625 -> 265000 floats)
    // Allocate generously to avoid truncation
    size_t output_capacity = 300000;
    output_data = (float*)malloc(output_capacity * sizeof(float));
    if (!output_data) {
        fprintf(stderr, "Failed to allocate output data buffer\n");
        free(output_shape);
        free(input_data);
        speedreader_ort_destroy_session(session);
        free(model_data);
        speedreader_ort_destroy_env(env);
        return 1;
    }

    SpeedReaderOrtFloatBuf output_data_buf = {
        .buffer = output_data,
        .capacity = output_capacity,
        .length = 0
    };

    SpeedReaderOrtInt64Buf output_shape_buf = {
        .buffer = output_shape,
        .capacity = 4,
        .length = 0
    };

    printf("   ✓ Output data buffer: %zu elements capacity\n", output_capacity);
    printf("   ✓ Output shape buffer: 4 dimensions capacity\n\n");

    // Step 6: Run inference
    printf("6. Running inference...\n");
    status = speedreader_ort_run(
        session,
        input_data,
        input_shape,
        4,  // input_shape_length
        &output_data_buf,
        &output_shape_buf,
        &error
    );

    if (status == SPEEDREADER_ORT_TRUNCATED) {
        fprintf(stderr, "ERROR: Output buffer too small\n");
        fprintf(stderr, "  Required shape length: %zu\n", output_shape_buf.length);
        fprintf(stderr, "  Required data length: %zu\n", output_data_buf.length);
        free(output_data);
        free(output_shape);
        free(input_data);
        speedreader_ort_destroy_session(session);
        free(model_data);
        speedreader_ort_destroy_env(env);
        return 1;
    }

    if (status != SPEEDREADER_ORT_OK) {
        print_error("run", &error);
        free(output_data);
        free(output_shape);
        free(input_data);
        speedreader_ort_destroy_session(session);
        free(model_data);
        speedreader_ort_destroy_env(env);
        return 1;
    }

    printf("   ✓ Inference completed successfully\n\n");

    // Step 7: Validate output
    printf("7. Validating output...\n");
    printf("   Output shape: [");
    for (size_t i = 0; i < output_shape_buf.length; i++) {
        printf("%ld", output_shape[i]);
        if (i < output_shape_buf.length - 1) {
            printf(", ");
        }
    }
    printf("]\n");
    printf("   Output data length: %zu elements\n", output_data_buf.length);

    // Basic sanity checks
    int validation_passed = 1;

    // Should have 3 dimensions (batch, timesteps, classes)
    if (output_shape_buf.length != 3) {
        fprintf(stderr, "   ✗ Expected 3 dimensions (batch, timesteps, classes), got %zu\n", output_shape_buf.length);
        validation_passed = 0;
    } else {
        printf("   ✓ Output has 3 dimensions (batch, timesteps, classes)\n");
    }

    // Batch dimension should be 1
    if (output_shape_buf.length >= 1 && output_shape[0] != 1) {
        fprintf(stderr, "   ✗ Expected batch size 1, got %ld\n", output_shape[0]);
        validation_passed = 0;
    } else if (output_shape_buf.length >= 1) {
        printf("   ✓ Batch dimension is 1\n");
    }

    // Third dimension should be OUTPUT_CLASSES
    if (output_shape_buf.length >= 3 && output_shape[2] != OUTPUT_CLASSES) {
        fprintf(stderr, "   ✗ Expected %d classes, got %ld\n", OUTPUT_CLASSES, output_shape[2]);
        validation_passed = 0;
    } else if (output_shape_buf.length >= 3) {
        printf("   ✓ Correct number of classes: %ld\n", output_shape[2]);
    }

    // Timesteps should be reasonable (not too large, not too small)
    if (output_shape_buf.length >= 2) {
        int64_t timesteps = output_shape[1];
        if (timesteps < 1 || timesteps > 100) {
            fprintf(stderr, "   ✗ Unexpected timesteps: %ld\n", timesteps);
            validation_passed = 0;
        } else {
            printf("   ✓ Timesteps: %ld\n", timesteps);
        }
    }

    // Data length should match shape
    if (output_shape_buf.length == 3) {
        size_t expected_length = output_shape[0] * output_shape[1] * output_shape[2];
        if (output_data_buf.length != expected_length) {
            fprintf(stderr, "   ✗ Expected %zu elements, got %zu\n", expected_length, output_data_buf.length);
            validation_passed = 0;
        } else {
            printf("   ✓ Data length matches shape\n");
        }
    }

    // Sample a few output values (should be finite)
    int has_nan_or_inf = 0;
    for (size_t i = 0; i < 10 && i < output_data_buf.length; i++) {
        if (!isfinite(output_data[i])) {
            has_nan_or_inf = 1;
            break;
        }
    }

    if (has_nan_or_inf) {
        fprintf(stderr, "   ✗ Output contains NaN or Inf values\n");
        validation_passed = 0;
    } else {
        printf("   ✓ Output values are finite\n");
    }

    printf("\n");

    // Step 8: Cleanup
    printf("8. Cleaning up...\n");
    free(output_data);
    free(output_shape);
    free(input_data);
    speedreader_ort_destroy_session(session);
    free(model_data);
    speedreader_ort_destroy_env(env);
    printf("   ✓ All resources released\n\n");

    // Final result
    if (validation_passed) {
        printf("=== ✓ SMOKE TEST PASSED ===\n");
        return 0;
    } else {
        printf("=== ✗ SMOKE TEST FAILED ===\n");
        return 1;
    }
}