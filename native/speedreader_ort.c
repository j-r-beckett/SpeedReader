// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

#include "speedreader_ort.h"
#include <onnxruntime_c_api.h>
#include <string.h>
#include <stdlib.h>

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

static void write_error_message(OrtStatus* status, SpeedReaderOrtStringBuf* error) {
    if (error == NULL || status == NULL) {
        if (status != NULL) {
            get_api()->ReleaseStatus(status);
        }
        return;
    }

    const OrtApi* api = get_api();
    const char* msg = api->GetErrorMessage(status);

    if (msg != NULL && error->buffer != NULL && error->capacity > 0) {
        size_t msg_len = strlen(msg);
        size_t copy_len = msg_len < error->capacity ? msg_len : error->capacity - 1;
        memcpy(error->buffer, msg, copy_len);
        error->buffer[copy_len] = '\0';
        error->length = copy_len;
    } else {
        error->length = 0;
    }

    api->ReleaseStatus(status);
}

// ************
// Environment management
// ************

SpeedReaderOrtStatus speedreader_ort_create_env(
    SpeedReaderOrtEnv** env,
    SpeedReaderOrtStringBuf* error
) {
    if (env == NULL) {
        return SPEEDREADER_ORT_INVALID_ARGUMENT;
    }

    const OrtApi* api = get_api();

    SpeedReaderOrtEnv* new_env = (SpeedReaderOrtEnv*)malloc(sizeof(SpeedReaderOrtEnv));
    if (new_env == NULL) {
        return SPEEDREADER_ORT_UNKNOWN;
    }

    OrtStatus* status = api->CreateEnv(ORT_LOGGING_LEVEL_WARNING, "SpeedReader", &new_env->ort_env);
    if (status != NULL) {
        free(new_env);
        write_error_message(status, error);
        return SPEEDREADER_ORT_UNKNOWN;
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
    size_t model_data_length,
    const SpeedReaderOrtSessionOptions* options,
    SpeedReaderOrtSession** session,
    SpeedReaderOrtStringBuf* error
) {
    if (env == NULL || model_data == NULL || options == NULL || session == NULL) {
        return SPEEDREADER_ORT_INVALID_ARGUMENT;
    }

    const OrtApi* api = get_api();
    OrtStatus* status = NULL;
    OrtSessionOptions* session_options = NULL;

    // Create session options
    status = api->CreateSessionOptions(&session_options);
    if (status != NULL) {
        write_error_message(status, error);
        return SPEEDREADER_ORT_UNKNOWN;
    }

    // Set thread configuration
    status = api->SetIntraOpNumThreads(session_options, options->intra_op_num_threads);
    if (status != NULL) {
        api->ReleaseSessionOptions(session_options);
        write_error_message(status, error);
        return SPEEDREADER_ORT_UNKNOWN;
    }

    status = api->SetInterOpNumThreads(session_options, options->inter_op_num_threads);
    if (status != NULL) {
        api->ReleaseSessionOptions(session_options);
        write_error_message(status, error);
        return SPEEDREADER_ORT_UNKNOWN;
    }

    // Enable profiling if requested
    if (options->enable_profiling != 0) {
        status = api->EnableProfiling(session_options, "speedreader_profile");
        if (status != NULL) {
            api->ReleaseSessionOptions(session_options);
            write_error_message(status, error);
            return SPEEDREADER_ORT_UNKNOWN;
        }
    }

    // Allocate session wrapper
    SpeedReaderOrtSession* new_session = (SpeedReaderOrtSession*)malloc(sizeof(SpeedReaderOrtSession));
    if (new_session == NULL) {
        api->ReleaseSessionOptions(session_options);
        return SPEEDREADER_ORT_UNKNOWN;
    }

    // Create session from model bytes
    status = api->CreateSessionFromArray(
        env->ort_env,
        model_data,
        model_data_length,
        session_options,
        &new_session->ort_session
    );

    api->ReleaseSessionOptions(session_options);

    if (status != NULL) {
        free(new_session);
        write_error_message(status, error);
        return SPEEDREADER_ORT_UNKNOWN;
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
    size_t input_shape_length,
    SpeedReaderOrtFloatBuf* output_data,
    SpeedReaderOrtInt64Buf* output_shape,
    SpeedReaderOrtStringBuf* error
) {
    if (session == NULL || input_data == NULL || input_shape == NULL ||
        output_data == NULL || output_shape == NULL) {
        return SPEEDREADER_ORT_INVALID_ARGUMENT;
    }

    if (output_data->buffer == NULL && output_data->capacity > 0) {
        return SPEEDREADER_ORT_INVALID_ARGUMENT;
    }

    if (output_shape->buffer == NULL && output_shape->capacity > 0) {
        return SPEEDREADER_ORT_INVALID_ARGUMENT;
    }

    const OrtApi* api = get_api();
    OrtStatus* status = NULL;
    OrtMemoryInfo* mem_info = NULL;
    OrtValue* input_tensor = NULL;
    OrtValue* output_tensor = NULL;
    OrtTensorTypeAndShapeInfo* shape_info = NULL;
    SpeedReaderOrtStatus result = SPEEDREADER_ORT_OK;

    // Create CPU memory info for input tensor
    status = api->CreateCpuMemoryInfo(OrtArenaAllocator, OrtMemTypeDefault, &mem_info);
    if (status != NULL) {
        write_error_message(status, error);
        return SPEEDREADER_ORT_UNKNOWN;
    }

    // Calculate input tensor size
    size_t input_element_count = 1;
    for (size_t i = 0; i < input_shape_length; i++) {
        input_element_count *= input_shape[i];
    }
    size_t input_size_bytes = input_element_count * sizeof(float);

    // Create input tensor from existing memory
    status = api->CreateTensorWithDataAsOrtValue(
        mem_info,
        (void*)input_data,
        input_size_bytes,
        input_shape,
        input_shape_length,
        ONNX_TENSOR_ELEMENT_DATA_TYPE_FLOAT,
        &input_tensor
    );

    api->ReleaseMemoryInfo(mem_info);

    if (status != NULL) {
        write_error_message(status, error);
        return SPEEDREADER_ORT_UNKNOWN;
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
        write_error_message(status, error);
        return SPEEDREADER_ORT_UNKNOWN;
    }

    // Get output tensor shape information
    status = api->GetTensorTypeAndShape(output_tensor, &shape_info);
    if (status != NULL) {
        api->ReleaseValue(output_tensor);
        write_error_message(status, error);
        return SPEEDREADER_ORT_UNKNOWN;
    }

    // Get number of dimensions
    size_t num_dims = 0;
    status = api->GetDimensionsCount(shape_info, &num_dims);
    if (status != NULL) {
        api->ReleaseTensorTypeAndShapeInfo(shape_info);
        api->ReleaseValue(output_tensor);
        write_error_message(status, error);
        return SPEEDREADER_ORT_UNKNOWN;
    }

    // Check if output shape buffer has sufficient capacity
    if (num_dims > output_shape->capacity) {
        api->ReleaseTensorTypeAndShapeInfo(shape_info);
        api->ReleaseValue(output_tensor);
        output_shape->length = num_dims;
        return SPEEDREADER_ORT_TRUNCATED;
    }

    // Get dimensions
    status = api->GetDimensions(shape_info, output_shape->buffer, num_dims);
    if (status != NULL) {
        api->ReleaseTensorTypeAndShapeInfo(shape_info);
        api->ReleaseValue(output_tensor);
        write_error_message(status, error);
        return SPEEDREADER_ORT_UNKNOWN;
    }
    output_shape->length = num_dims;

    // Get total element count
    size_t output_element_count = 0;
    status = api->GetTensorShapeElementCount(shape_info, &output_element_count);

    api->ReleaseTensorTypeAndShapeInfo(shape_info);
    shape_info = NULL;

    if (status != NULL) {
        api->ReleaseValue(output_tensor);
        write_error_message(status, error);
        return SPEEDREADER_ORT_UNKNOWN;
    }

    // Check if output data buffer has sufficient capacity
    if (output_element_count > output_data->capacity) {
        api->ReleaseValue(output_tensor);
        output_data->length = output_element_count;
        return SPEEDREADER_ORT_TRUNCATED;
    }

    // Get pointer to output data
    float* output_ptr = NULL;
    status = api->GetTensorMutableData(output_tensor, (void**)&output_ptr);
    if (status != NULL) {
        api->ReleaseValue(output_tensor);
        write_error_message(status, error);
        return SPEEDREADER_ORT_UNKNOWN;
    }

    // Copy output data to caller's buffer
    memcpy(output_data->buffer, output_ptr, output_element_count * sizeof(float));
    output_data->length = output_element_count;

    api->ReleaseValue(output_tensor);
    return SPEEDREADER_ORT_OK;
}
