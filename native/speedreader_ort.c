// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

#include "speedreader_ort.h"
#include <onnxruntime_c_api.h>
#include <string.h>
#include <stdlib.h>
#include <stdio.h>

// ************
// Opaque handle internal structures
// ************

struct SpeedReaderOrtEnv {
    OrtEnv* ort_env;
};

struct SpeedReaderOrtSession {
    OrtSession* ort_session;
};

// ************
// Global API access
// ************

static const OrtApi* get_api(void) {
    static const OrtApi* api = NULL;
    if (api == NULL) {
        const OrtApiBase* api_base = OrtGetApiBase();
        api = api_base->GetApi(ORT_API_VERSION);
    }
    return api;
}

// ************
// Error handling helpers
// ************

static void write_error(char* error, const char* msg) {
    if (error == NULL || msg == NULL) {
        return;
    }
    size_t msg_len = strlen(msg);
    size_t copy_len = msg_len < SPEEDREADER_ORT_ERROR_BUF_SIZE - 1
                    ? msg_len
                    : SPEEDREADER_ORT_ERROR_BUF_SIZE - 1;
    memcpy(error, msg, copy_len);
    error[copy_len] = '\0';
}

static void write_ort_error(OrtStatus* status, char* error) {
    if (status == NULL) {
        return;
    }

    const OrtApi* api = get_api();
    const char* msg = api->GetErrorMessage(status);
    write_error(error, msg);
    api->ReleaseStatus(status);
}

static void clear_error(char* error) {
    if (error != NULL) {
        error[0] = '\0';
    }
}

// ************
// Environment management
// ************

SpeedReaderOrtStatus speedreader_ort_create_env(
    SpeedReaderOrtEnv** env,
    char* error
) {
    clear_error(error);

    if (env == NULL) {
        write_error(error, "env parameter is NULL");
        return SPEEDREADER_ORT_ERROR;
    }

    const OrtApi* api = get_api();

    SpeedReaderOrtEnv* new_env = (SpeedReaderOrtEnv*)malloc(sizeof(SpeedReaderOrtEnv));
    if (new_env == NULL) {
        write_error(error, "failed to allocate environment");
        return SPEEDREADER_ORT_ERROR;
    }

    OrtStatus* status = api->CreateEnv(ORT_LOGGING_LEVEL_WARNING, "SpeedReader", &new_env->ort_env);
    if (status != NULL) {
        free(new_env);
        write_ort_error(status, error);
        return SPEEDREADER_ORT_ERROR;
    }

    *env = new_env;
    return SPEEDREADER_ORT_OK;
}

void speedreader_ort_destroy_env(SpeedReaderOrtEnv* env) {
    if (env == NULL) {
        return;
    }

    const OrtApi* api = get_api();
    api->ReleaseEnv(env->ort_env);
    free(env);
}

// ************
// Session management
// ************

SpeedReaderOrtStatus speedreader_ort_create_session(
    SpeedReaderOrtEnv* env,
    const void* model_data,
    size_t model_data_size,
    const SpeedReaderOrtSessionOptions* options,
    SpeedReaderOrtSession** session,
    char* error
) {
    clear_error(error);

    if (env == NULL || model_data == NULL || options == NULL || session == NULL) {
        write_error(error, "invalid argument: NULL parameter");
        return SPEEDREADER_ORT_ERROR;
    }

    const OrtApi* api = get_api();
    OrtStatus* status = NULL;
    OrtSessionOptions* session_options = NULL;

    // Create session options
    status = api->CreateSessionOptions(&session_options);
    if (status != NULL) {
        write_ort_error(status, error);
        return SPEEDREADER_ORT_ERROR;
    }

    // Set thread configuration
    status = api->SetIntraOpNumThreads(session_options, options->intra_op_num_threads);
    if (status != NULL) {
        api->ReleaseSessionOptions(session_options);
        write_ort_error(status, error);
        return SPEEDREADER_ORT_ERROR;
    }

    status = api->SetInterOpNumThreads(session_options, options->inter_op_num_threads);
    if (status != NULL) {
        api->ReleaseSessionOptions(session_options);
        write_ort_error(status, error);
        return SPEEDREADER_ORT_ERROR;
    }

    // Enable profiling if requested
    if (options->enable_profiling != 0) {
        status = api->EnableProfiling(session_options, "speedreader_profile");
        if (status != NULL) {
            api->ReleaseSessionOptions(session_options);
            write_ort_error(status, error);
            return SPEEDREADER_ORT_ERROR;
        }
    }

    // Allocate session wrapper
    SpeedReaderOrtSession* new_session = (SpeedReaderOrtSession*)malloc(sizeof(SpeedReaderOrtSession));
    if (new_session == NULL) {
        api->ReleaseSessionOptions(session_options);
        write_error(error, "failed to allocate session");
        return SPEEDREADER_ORT_ERROR;
    }

    // Create session from model bytes
    status = api->CreateSessionFromArray(
        env->ort_env,
        model_data,
        model_data_size,
        session_options,
        &new_session->ort_session
    );

    api->ReleaseSessionOptions(session_options);

    if (status != NULL) {
        free(new_session);
        write_ort_error(status, error);
        return SPEEDREADER_ORT_ERROR;
    }

    *session = new_session;
    return SPEEDREADER_ORT_OK;
}

void speedreader_ort_destroy_session(SpeedReaderOrtSession* session) {
    if (session == NULL) {
        return;
    }

    const OrtApi* api = get_api();
    api->ReleaseSession(session->ort_session);
    free(session);
}

// ************
// Inference execution
// ************

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
) {
    clear_error(error);

    if (session == NULL || input_data == NULL || input_shape == NULL ||
        output_data == NULL || output_shape == NULL || output_ndim == NULL) {
        write_error(error, "invalid argument: NULL parameter");
        return SPEEDREADER_ORT_ERROR;
    }

    const OrtApi* api = get_api();
    OrtStatus* status = NULL;
    OrtMemoryInfo* mem_info = NULL;
    OrtValue* input_tensor = NULL;
    OrtValue* output_tensor = NULL;
    OrtTensorTypeAndShapeInfo* shape_info = NULL;

    // Create CPU memory info for input tensor
    status = api->CreateCpuMemoryInfo(OrtArenaAllocator, OrtMemTypeDefault, &mem_info);
    if (status != NULL) {
        write_ort_error(status, error);
        return SPEEDREADER_ORT_ERROR;
    }

    // Calculate input tensor size
    size_t input_element_count = 1;
    for (size_t i = 0; i < input_ndim; i++) {
        input_element_count *= input_shape[i];
    }
    size_t input_size_bytes = input_element_count * sizeof(float);

    // Create input tensor from existing memory
    status = api->CreateTensorWithDataAsOrtValue(
        mem_info,
        (void*)input_data,
        input_size_bytes,
        input_shape,
        input_ndim,
        ONNX_TENSOR_ELEMENT_DATA_TYPE_FLOAT,
        &input_tensor
    );

    api->ReleaseMemoryInfo(mem_info);

    if (status != NULL) {
        write_ort_error(status, error);
        return SPEEDREADER_ORT_ERROR;
    }

    // Run inference
    const char* input_names[] = {"input"};
    const char* output_names[] = {"output"};

    status = api->Run(
        session->ort_session,
        NULL,  // run_options
        input_names,
        (const OrtValue* const*)&input_tensor,
        1,  // num_inputs
        output_names,
        1,  // num_outputs
        &output_tensor
    );

    api->ReleaseValue(input_tensor);
    input_tensor = NULL;

    if (status != NULL) {
        write_ort_error(status, error);
        return SPEEDREADER_ORT_ERROR;
    }

    // Get output tensor shape information
    status = api->GetTensorTypeAndShape(output_tensor, &shape_info);
    if (status != NULL) {
        api->ReleaseValue(output_tensor);
        write_ort_error(status, error);
        return SPEEDREADER_ORT_ERROR;
    }

    // Get number of dimensions
    size_t num_dims = 0;
    status = api->GetDimensionsCount(shape_info, &num_dims);
    if (status != NULL) {
        api->ReleaseTensorTypeAndShapeInfo(shape_info);
        api->ReleaseValue(output_tensor);
        write_ort_error(status, error);
        return SPEEDREADER_ORT_ERROR;
    }

    // Check if output shape buffer has sufficient capacity
    if (num_dims > SPEEDREADER_ORT_MAX_SHAPE_DIMS) {
        api->ReleaseTensorTypeAndShapeInfo(shape_info);
        api->ReleaseValue(output_tensor);
        snprintf(error, SPEEDREADER_ORT_ERROR_BUF_SIZE,
                 "output has %zu dimensions, max supported is %d",
                 num_dims, SPEEDREADER_ORT_MAX_SHAPE_DIMS);
        return SPEEDREADER_ORT_ERROR;
    }

    // Get dimensions
    status = api->GetDimensions(shape_info, output_shape, num_dims);
    if (status != NULL) {
        api->ReleaseTensorTypeAndShapeInfo(shape_info);
        api->ReleaseValue(output_tensor);
        write_ort_error(status, error);
        return SPEEDREADER_ORT_ERROR;
    }
    *output_ndim = num_dims;

    // Get total element count
    size_t output_element_count = 0;
    status = api->GetTensorShapeElementCount(shape_info, &output_element_count);

    api->ReleaseTensorTypeAndShapeInfo(shape_info);
    shape_info = NULL;

    if (status != NULL) {
        api->ReleaseValue(output_tensor);
        write_ort_error(status, error);
        return SPEEDREADER_ORT_ERROR;
    }

    // Verify output data size matches exactly
    if (output_element_count != output_count) {
        api->ReleaseValue(output_tensor);
        snprintf(error, SPEEDREADER_ORT_ERROR_BUF_SIZE,
                 "output size mismatch: expected %zu, got %zu",
                 output_count, output_element_count);
        return SPEEDREADER_ORT_ERROR;
    }

    // Get pointer to output data
    float* output_ptr = NULL;
    status = api->GetTensorMutableData(output_tensor, (void**)&output_ptr);
    if (status != NULL) {
        api->ReleaseValue(output_tensor);
        write_ort_error(status, error);
        return SPEEDREADER_ORT_ERROR;
    }

    // Copy output data to caller's buffer
    memcpy(output_data, output_ptr, output_element_count * sizeof(float));

    api->ReleaseValue(output_tensor);
    return SPEEDREADER_ORT_OK;
}
