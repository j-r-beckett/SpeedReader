#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "onnx", "numpy", "build_utils"]
#
# [tool.uv.sources]
# build_utils = { path = "../build_utils", editable = true }
# ///

from pathlib import Path
from collections import Counter
import click
import onnx
import numpy as np
from build_utils import ScriptError, bash, error


def format_shape(shape) -> str:
    """Format a tensor shape, handling dynamic dimensions"""
    dims = []
    for dim in shape.dim:
        if dim.HasField("dim_value"):
            dims.append(str(dim.dim_value))
        elif dim.HasField("dim_param"):
            dims.append(dim.dim_param)
        else:
            dims.append("?")
    return f"[{', '.join(dims)}]"


# (name, size_in_bytes) for each ONNX tensor type
DTYPE_INFO = {
    onnx.TensorProto.FLOAT: ("float32", 4),
    onnx.TensorProto.UINT8: ("uint8", 1),
    onnx.TensorProto.INT8: ("int8", 1),
    onnx.TensorProto.UINT16: ("uint16", 2),
    onnx.TensorProto.INT16: ("int16", 2),
    onnx.TensorProto.INT32: ("int32", 4),
    onnx.TensorProto.INT64: ("int64", 8),
    onnx.TensorProto.BOOL: ("bool", 1),
    onnx.TensorProto.FLOAT16: ("float16", 2),
    onnx.TensorProto.DOUBLE: ("float64", 8),
    onnx.TensorProto.UINT32: ("uint32", 4),
    onnx.TensorProto.UINT64: ("uint64", 8),
    onnx.TensorProto.BFLOAT16: ("bfloat16", 2),
}


def format_dtype(elem_type: int) -> str:
    """Convert ONNX element type to human-readable string"""
    info = DTYPE_INFO.get(elem_type)
    return info[0] if info else f"unknown({elem_type})"


def dtype_size(elem_type: int) -> int:
    """Return size in bytes for an ONNX element type"""
    info = DTYPE_INFO.get(elem_type)
    return info[1] if info else 4


def format_size(size_bytes: int) -> str:
    """Format byte size as human-readable string"""
    if size_bytes >= 1024 * 1024 * 1024:
        return f"{size_bytes / (1024 * 1024 * 1024):.2f} GB"
    elif size_bytes >= 1024 * 1024:
        return f"{size_bytes / (1024 * 1024):.2f} MB"
    elif size_bytes >= 1024:
        return f"{size_bytes / 1024:.2f} KB"
    return f"{size_bytes} bytes"


def count_parameters(model: onnx.ModelProto) -> tuple[int, dict[str, int], int]:
    """Count total parameters, breakdown by data type, and total memory size"""
    total = 0
    total_bytes = 0
    by_dtype = Counter()

    for initializer in model.graph.initializer:
        param_count = int(np.prod(initializer.dims)) if initializer.dims else 1
        total += param_count
        dtype_name = format_dtype(initializer.data_type)
        by_dtype[dtype_name] += param_count
        total_bytes += param_count * dtype_size(initializer.data_type)

    return total, dict(by_dtype), total_bytes


def validate_model(model: onnx.ModelProto) -> str | None:
    """Validate model and return error message if invalid"""
    try:
        onnx.checker.check_model(model)
        return None
    except onnx.checker.ValidationError as e:
        return str(e)


def analyze_model(model_path: Path, open_netron: bool = False):
    """Analyze an ONNX model and print its properties"""
    file_size = model_path.stat().st_size
    model = onnx.load(str(model_path))

    if validation_error := validate_model(model):
        raise ScriptError(f"Invalid model: {validation_error}")

    # Metadata
    print("--- Metadata ---")
    print(f"Size: {format_size(file_size)}")
    print(f"IR Version: {model.ir_version}")
    print(f"Opset Version: {model.opset_import[0].version if model.opset_import else 'unknown'}")

    # Input/Output Tensors
    print("\n--- Tensors ---")
    initializer_names = {init.name for init in model.graph.initializer}
    for inp in model.graph.input:
        if inp.name not in initializer_names:
            shape = format_shape(inp.type.tensor_type.shape)
            dtype = format_dtype(inp.type.tensor_type.elem_type)
            print(f'In:  "{inp.name}": {dtype} {shape}')
    for out in model.graph.output:
        shape = format_shape(out.type.tensor_type.shape)
        dtype = format_dtype(out.type.tensor_type.elem_type)
        print(f'Out: "{out.name}": {dtype} {shape}')

    # Parameters
    total_params, params_by_dtype, param_memory = count_parameters(model)
    print("\n--- Parameters ---")
    print(f"Count: {total_params:,}")
    print(f"Size: {format_size(param_memory)}")
    if params_by_dtype:
        print("By dtype:")
        for dtype, count in sorted(params_by_dtype.items(), key=lambda x: -x[1]):
            pct = count / total_params * 100
            print(f"  {dtype}: {count:,} ({pct:.1f}%)")

    # Operator distribution
    op_counts = Counter(node.op_type for node in model.graph.node)
    print("\n--- Operators ---")
    for op, count in op_counts.most_common():
        print(f"{op}: {count}")
    print("---")
    print(f"Total: {len(model.graph.node)}")

    if open_netron:
        print()
        bash(f"netron --browse {model_path.resolve()}")


@click.command()
@click.argument("model_path", type=click.Path(exists=True, path_type=Path))
@click.option("--netron", is_flag=True, help="Open model in Netron visualizer")
def main(model_path: Path, netron: bool):
    """Analyze an ONNX model and display its properties.

    MODEL_PATH: Path to the ONNX model file to analyze.
    """
    analyze_model(model_path, open_netron=netron)


if __name__ == "__main__":
    try:
        main()
    except ScriptError as e:
        error(f"Fatal: {e}")
        exit(1)
