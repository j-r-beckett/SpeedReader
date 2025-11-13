# ONNX Runtime C API Notes

**Header File:** `~/library/onnxruntime/include/onnxruntime/core/session/onnxruntime_c_api.h`

## Overview

The ONNX Runtime C API is exposed through a function pointer table (`OrtApi`) accessed via `OrtGetApiBase()`. This indirection provides ABI stability across versions.

**API Version:** 24 (defined as `ORT_API_VERSION`)

## Core Concepts

### Error Handling

All API functions return `OrtStatus*`:
- `NULL` = success
- Non-NULL = error (must be freed with `ReleaseStatus`)
- Extract error code with `GetErrorCode(status)`
- Extract error message with `GetErrorMessage(status)`

### Object Lifetime

All objects follow create/release pattern:
- Created via API functions (e.g., `CreateEnv`, `CreateSession`)
- Must be explicitly freed with corresponding `Release*` function
- Release functions accept NULL pointers (safe to call unconditionally)

### Key Types

- **`OrtEnv`**: Global environment (singleton pattern - repeated creation returns same instance)
- **`OrtSession`**: Inference session wrapping a loaded model
- **`OrtSessionOptions`**: Configuration for session creation (threads, profiling, execution mode)
- **`OrtValue`**: Tensor wrapper for input/output data
- **`OrtAllocator`**: Memory allocator interface
- **`OrtTensorTypeAndShapeInfo`**: Metadata about tensor shape and type

## Initialization Pattern

```c
// Get API function table
const OrtApiBase* api_base = OrtGetApiBase();
const OrtApi* api = api_base->GetApi(ORT_API_VERSION);

// Create environment (singleton)
OrtEnv* env;
OrtStatus* status = api->CreateEnv(ORT_LOGGING_LEVEL_WARNING, "app_name", &env);
// Check status...

// Cleanup at shutdown
api->ReleaseEnv(env);
```

## Session Creation

### From Memory

```c
OrtSessionOptions* options;
api->CreateSessionOptions(&options);
api->SetIntraOpNumThreads(options, 4);
api->SetInterOpNumThreads(options, 1);

OrtSession* session;
api->CreateSessionFromArray(env, model_bytes, model_size, options, &session);

api->ReleaseSessionOptions(options);
// ... use session ...
api->ReleaseSession(session);
```

**Thread Configuration:**
- **IntraOpNumThreads**: Parallelism within a single operator (e.g., matrix multiplication)
- **InterOpNumThreads**: Parallelism across independent operators in the graph

### Session Introspection

```c
// Get input/output counts
size_t num_inputs, num_outputs;
api->SessionGetInputCount(session, &num_inputs);
api->SessionGetOutputCount(session, &num_outputs);

// Get input/output names (requires allocator)
OrtAllocator* allocator;
api->GetAllocatorWithDefaultOptions(&allocator);

char* input_name;
api->SessionGetInputName(session, 0, allocator, &input_name);
// ... use input_name ...
allocator->Free(allocator, input_name);
```

## Tensor Operations

### Creating Input Tensors

**Option 1: From Existing Memory** (zero-copy, caller manages lifetime)
```c
OrtMemoryInfo* mem_info;
api->CreateCpuMemoryInfo(OrtArenaAllocator, OrtMemTypeDefault, &mem_info);

float* input_data = /* your data */;
int64_t shape[] = {1, 3, 224, 224};  // NCHW format

OrtValue* input_tensor;
api->CreateTensorWithDataAsOrtValue(
    mem_info,
    input_data,
    data_size_bytes,
    shape,
    4,  // shape length
    ONNX_TENSOR_ELEMENT_DATA_TYPE_FLOAT,
    &input_tensor
);

api->ReleaseMemoryInfo(mem_info);
// ... use tensor ...
api->ReleaseValue(input_tensor);
```

**Key Detail:** The `input_data` memory must remain valid until after inference completes.

### Running Inference

```c
const char* input_names[] = {"input"};
const char* output_names[] = {"output"};

OrtValue* input_tensors[] = {input_tensor};
OrtValue* output_tensors[] = {NULL};  // API allocates outputs

api->Run(
    session,
    NULL,  // run_options (NULL for defaults)
    input_names,
    (const OrtValue* const*)input_tensors,
    1,  // num_inputs
    output_names,
    1,  // num_outputs
    output_tensors
);

// output_tensors[0] now contains result
```

**Thread Safety:** `Run()` is thread-safe. Multiple threads can call `Run()` on the same session concurrently.

### Extracting Output Data

```c
OrtValue* output = output_tensors[0];

// Get shape information
OrtTensorTypeAndShapeInfo* shape_info;
api->GetTensorTypeAndShape(output, &shape_info);

size_t num_dims;
api->GetDimensionsCount(shape_info, &num_dims);

int64_t* dims = malloc(num_dims * sizeof(int64_t));
api->GetDimensions(shape_info, dims, num_dims);

size_t total_elements;
api->GetTensorShapeElementCount(shape_info, &total_elements);

api->ReleaseTensorTypeAndShapeInfo(shape_info);

// Get data pointer
float* output_data;
api->GetTensorMutableData(output, (void**)&output_data);

// Copy data out
float* result = malloc(total_elements * sizeof(float));
memcpy(result, output_data, total_elements * sizeof(float));

// Cleanup
api->ReleaseValue(output);
free(dims);
```

## Memory Management Notes

### Allocator Usage

The default allocator is sufficient for most use cases:
```c
OrtAllocator* allocator;
api->GetAllocatorWithDefaultOptions(&allocator);
```

This allocator is used for:
- Allocating output tensors when passing NULL in `outputs` array to `Run()`
- Getting session input/output names
- Internal ONNX Runtime allocations

**Do not free the default allocator** - it's managed by ONNX Runtime.

### OrtValue Ownership

When creating tensors with `CreateTensorWithDataAsOrtValue`:
- ONNX Runtime does **not** take ownership of the data pointer
- Caller must ensure data remains valid during inference
- `ReleaseValue()` only releases the `OrtValue` wrapper, not the data

When ONNX Runtime creates outputs:
- It allocates memory for output tensors
- `GetTensorMutableData()` returns a pointer to this memory
- Memory is freed when `ReleaseValue()` is called on the output tensor
- Copy data out before releasing if you need it

## Relevant Details for SpeedReader

### Single Input, Single Output

SpeedReader models have:
- Input name: `"input"`
- Output name: varies by model (use `SessionGetOutputName` to query)
- Input shape: `[1, C, H, W]` (batch=1, channels, height, width)
- Output shape: Model-dependent

### Float32 Tensors Only

All SpeedReader models use `ONNX_TENSOR_ELEMENT_DATA_TYPE_FLOAT` (32-bit floats).

### Shape Handling

Input shapes include batch dimension (always 1 for SpeedReader):
- C# passes shape without batch dimension: `[C, H, W]`
- C wrapper must prepend 1: `[1, C, H, W]`
- Output shape includes batch dimension
- C wrapper must strip batch dimension before returning to C#

### Parallel Inference

The current C# implementation runs multiple inference sessions concurrently:
- Each session is independent
- `Run()` calls happen in parallel on different threads
- This is explicitly supported and thread-safe

## Minimal API Surface for SpeedReader

Required functions:
1. `OrtGetApiBase` / `GetApi` - Initialization
2. `CreateEnv` / `ReleaseEnv` - Environment
3. `CreateSessionOptions` / `ReleaseSessionOptions` - Configuration
4. `SetIntraOpNumThreads` / `SetInterOpNumThreads` - Thread config
5. `CreateSessionFromArray` / `ReleaseSession` - Session management
6. `CreateCpuMemoryInfo` / `ReleaseMemoryInfo` - Memory info for tensors
7. `CreateTensorWithDataAsOrtValue` / `ReleaseValue` - Tensor creation
8. `Run` - Inference execution
9. `GetTensorTypeAndShape` / `ReleaseTensorTypeAndShapeInfo` - Output shape
10. `GetDimensionsCount` / `GetDimensions` - Shape extraction
11. `GetTensorMutableData` - Output data access
12. `GetErrorCode` / `GetErrorMessage` / `ReleaseStatus` - Error handling

Optional but useful:
- `SessionGetOutputName` - Query output name instead of hardcoding
- `GetAllocatorWithDefaultOptions` - If using dynamic output names

## Common Pitfalls

1. **Forgetting to check status**: All API calls can fail, always check return value
2. **Memory leaks**: Every Create*/Get* must have corresponding Release*
3. **Data lifetime**: Input data must outlive the `Run()` call
4. **Shape dimensions**: Remember batch dimension differences between C# and ONNX
5. **NULL pointers**: Most API functions do not accept NULL (except release functions)
6. **Thread safety**: `OrtSessionOptions` is not thread-safe during configuration (create per-session)

## Build Considerations

When linking statically:
- Must link with C++ standard library (ONNX Runtime is C++)
- Must link with pthread on Linux
- Models are embedded in C# binary, not loaded from disk
- No execution providers configured = CPU-only inference
