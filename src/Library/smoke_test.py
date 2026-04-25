#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# ///

import ctypes
import json
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent.resolve()
LIB_PATH = SCRIPT_DIR / "bin/Release/net10.0/linux-x64/publish/Library.so"
IMAGE_PATH = SCRIPT_DIR.parent.parent / "hello.png"

ERROR_BUF_SIZE = 256
OK = 0
ERROR = 1
TIMEOUT = 2

lib = ctypes.CDLL(str(LIB_PATH))

# speedreader_create
lib.speedreader_create.argtypes = [ctypes.POINTER(ctypes.c_int64), ctypes.c_char_p]
lib.speedreader_create.restype = ctypes.c_int

# speedreader_destroy
lib.speedreader_destroy.argtypes = [ctypes.c_int64]
lib.speedreader_destroy.restype = None

# speedreader_submit
lib.speedreader_submit.argtypes = [
    ctypes.c_int64, ctypes.POINTER(ctypes.c_uint8), ctypes.c_size_t,
    ctypes.POINTER(ctypes.c_int64), ctypes.c_char_p,
]
lib.speedreader_submit.restype = ctypes.c_int

# speedreader_await
lib.speedreader_await.argtypes = [
    ctypes.c_int64, ctypes.c_int64, ctypes.c_int32,
    ctypes.POINTER(ctypes.POINTER(ctypes.c_uint8)), ctypes.POINTER(ctypes.c_size_t),
    ctypes.c_char_p,
]
lib.speedreader_await.restype = ctypes.c_int

# speedreader_free_result
lib.speedreader_free_result.argtypes = [ctypes.POINTER(ctypes.c_uint8)]
lib.speedreader_free_result.restype = None


def check(status, error_buf, context):
    if status != OK:
        msg = error_buf.value.decode() if error_buf else "unknown error"
        raise RuntimeError(f"{context}: {msg}")


def main():
    error = ctypes.create_string_buffer(ERROR_BUF_SIZE)

    # Create
    instance = ctypes.c_int64()
    check(lib.speedreader_create(ctypes.byref(instance), error), error, "create")
    print(f"created instance: {instance.value}")

    # Submit
    image_data = IMAGE_PATH.read_bytes()
    image_buf = (ctypes.c_uint8 * len(image_data))(*image_data)
    handle = ctypes.c_int64()
    check(
        lib.speedreader_submit(instance, image_buf, len(image_data), ctypes.byref(handle), error),
        error, "submit",
    )
    print(f"submitted image: handle={handle.value}")

    # Await
    result_ptr = ctypes.POINTER(ctypes.c_uint8)()
    result_len = ctypes.c_size_t()
    check(
        lib.speedreader_await(instance, handle, -1, ctypes.byref(result_ptr), ctypes.byref(result_len), error),
        error, "await",
    )
    result_json = ctypes.string_at(result_ptr, result_len.value).decode()
    lib.speedreader_free_result(result_ptr)

    result = json.loads(result_json)
    print(json.dumps(result, indent=2))

    # Destroy
    lib.speedreader_destroy(instance)
    print("destroyed instance")


if __name__ == "__main__":
    main()
