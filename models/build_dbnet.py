#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "onnx", "onnxruntime", "onnxsim", "pillow", "numpy", "utils"]
#
# [tool.uv.sources]
# utils = { path = "../tools/utils", editable = true }
# ///

import os
import time
import shutil
import urllib.request
from pathlib import Path
import numpy as np
from PIL import Image
import onnxruntime
from onnxruntime.quantization import quantize_static, CalibrationDataReader, QuantType
from utils import ScriptError, bash, info, error, format_duration, checkout_submodule
from download_dataset import download_dataset


class CalibrationDataset(CalibrationDataReader):
    """Calibration dataset reader for DBNet quantization"""

    def __init__(self, calibration_dir: Path, max_images: int = None):
        self.calibration_dir = calibration_dir
        self.image_files = list(self.calibration_dir.glob("*.jpg")) + list(self.calibration_dir.glob("*.png"))

        if max_images:
            self.image_files = self.image_files[:max_images]

        info(f"Using {len(self.image_files)} calibration images")
        self.current_index = 0

    def get_next(self):
        if self.current_index >= len(self.image_files):
            return None

        image_path = self.image_files[self.current_index]
        self.current_index += 1

        # Load and preprocess image for DBNet
        image = Image.open(image_path).convert('RGB')
        image = image.resize((640, 640))

        # Convert to numpy array and normalize
        image_array = np.array(image, dtype=np.float32)
        image_array = image_array.transpose(2, 0, 1)  # HWC to CHW
        image_array = image_array / 255.0  # Normalize to [0, 1]

        # Add batch dimension
        image_array = np.expand_dims(image_array, axis=0)

        return {'input': image_array}


def create_mmdeploy_venv(mmdeploy_dir: Path) -> Path:
    """Create and configure Python environment for mmdeploy"""
    # Use .venv which is gitignored by mmdeploy
    venv_dir = mmdeploy_dir / ".venv"

    if not venv_dir.exists():
        info("Creating mmdeploy python environment")
        # Use Python 3.10 (mmcv 2.0.1 only supports Python 3.7-3.10, not 3.11+)
        bash(f"uv venv {venv_dir} --python 3.10", directory=mmdeploy_dir)

    info("Installing mmdeploy dependencies")
    # Install pip in venv (needed for mmcv installation from OpenMMLab index)
    bash(
        f"uv pip install --python {venv_dir}/bin/python pip",
        directory=mmdeploy_dir,
    )
    # Install dependencies matching the Dockerfile
    # Pin numpy<2.0 for compatibility with torch 2.0.0
    bash(
        f"uv pip install --python {venv_dir}/bin/python "
        f"'numpy<2.0' torch==2.0.0 torchvision==0.15.0 --index-url https://download.pytorch.org/whl/cpu",
        directory=mmdeploy_dir,
    )
    bash(
        f"uv pip install --python {venv_dir}/bin/python "
        f"mmengine==0.7.4",
        directory=mmdeploy_dir,
    )
    # Install mmcv from OpenMMLab's pre-built wheel repository
    # PyPI doesn't have wheels for our specific torch/python combination
    # The wheel includes pre-compiled C++ extensions
    # Use pip with --find-links (same as Dockerfile) since it's a simple HTML page, not a PyPI index
    bash(
        f"{venv_dir}/bin/pip install "
        f"mmcv==2.0.1 -f https://download.openmmlab.com/mmcv/dist/cpu/torch2.0/index.html",
        directory=mmdeploy_dir,
    )
    bash(
        f"uv pip install --python {venv_dir}/bin/python "
        f"mmdet==3.0.0 mmocr==1.0.1 mmdeploy==1.3.1 onnxruntime==1.15.0",
        directory=mmdeploy_dir,
    )

    return venv_dir


def download_checkpoint(checkpoint_dir: Path, checkpoint_url: str, checkpoint_name: str):
    """Download model checkpoint if it doesn't exist"""
    checkpoint_path = checkpoint_dir / checkpoint_name

    if checkpoint_path.exists():
        info(f"Checkpoint already exists at {checkpoint_path}")
        return checkpoint_path

    checkpoint_dir.mkdir(parents=True, exist_ok=True)
    info(f"Downloading checkpoint from {checkpoint_url}")

    try:
        urllib.request.urlretrieve(checkpoint_url, checkpoint_path)
    except Exception as e:
        raise ScriptError(f"Failed to download checkpoint: {e}")

    if not checkpoint_path.exists():
        raise ScriptError("Failed to download checkpoint")

    return checkpoint_path


def build_dbnet_model(mmdeploy_dir: Path, mmocr_dir: Path, venv_dir: Path, checkpoint_path: Path, work_dir: Path):
    """Build DBNet ONNX model using mmdeploy"""
    info("Building DBNet FP32 model")

    # Clean work directory
    if work_dir.exists():
        shutil.rmtree(work_dir)
    work_dir.mkdir(parents=True, exist_ok=True)

    # Run mmdeploy's deploy.py (same command as Dockerfile)
    demo_image = mmocr_dir / "demo" / "demo_text_det.jpg"
    if not demo_image.exists():
        raise ScriptError(f"Demo image not found at {demo_image}")

    bash(
        f"source {venv_dir}/bin/activate && "
        f"python3 {mmdeploy_dir}/tools/deploy.py "
        f"{mmdeploy_dir}/configs/mmocr/text-detection/text-detection_onnxruntime_dynamic.py "
        f"{mmocr_dir}/configs/textdet/dbnet/dbnet_resnet18_fpnc_1200e_icdar2015.py "
        f"{checkpoint_path} "
        f"{demo_image} "
        f"--work-dir {work_dir} "
        f"--log-level INFO "
        f"--dump-info"
    )

    # Check if model was created
    model_path = work_dir / "end2end.onnx"
    if not model_path.exists():
        raise ScriptError(f"Model not found at {model_path}")

    return model_path


def quantize_dbnet(fp32_model_path: Path, calibration_dir: Path, max_images: int = 100):
    """Quantize DBNet FP32 model to INT8"""
    info(f"Quantizing DBNet model with {max_images} calibration images")

    # Create calibration data reader
    calibration_data_reader = CalibrationDataset(calibration_dir, max_images)

    # Output path for quantized model
    int8_model_path = fp32_model_path.with_name(
        fp32_model_path.name.replace("_fp32.onnx", "_int8.onnx")
    )

    start_time = time.time()
    quantize_static(
        model_input=str(fp32_model_path),
        model_output=str(int8_model_path),
        calibration_data_reader=calibration_data_reader,
        quant_format=onnxruntime.quantization.QuantFormat.QDQ,
        weight_type=QuantType.QInt8,
        activation_type=QuantType.QUInt8
    )
    elapsed_time = time.time() - start_time

    info(f"Quantized model created in {format_duration(elapsed_time)}: {int8_model_path}")
    return int8_model_path


def build_dbnet():
    """Build DBNet FP32 model end-to-end"""
    start_time = time.time()

    # Setup directories
    models_dir = Path(__file__).parent.resolve()
    repo_root = models_dir.parent
    datasets_dir = repo_root / "datasets"
    checkpoint_dir = models_dir / "checkpoints"

    mmdeploy_dir = models_dir / "external" / "mmdeploy"
    mmocr_dir = models_dir / "external" / "mmocr"
    # Use work_dirs/ which is gitignored by mmdeploy
    work_dir = mmdeploy_dir / "work_dirs" / "dbnet"

    # Checkout submodules
    checkout_submodule(mmdeploy_dir, "v1.3.1", "mmdeploy")
    checkout_submodule(mmocr_dir, "v1.0.1", "mmocr")

    # Create Python environment
    venv_dir = create_mmdeploy_venv(mmdeploy_dir)

    # Download checkpoint
    checkpoint_url = "https://download.openmmlab.com/mmocr/textdet/dbnet/dbnet_resnet18_fpnc_1200e_icdar2015/dbnet_resnet18_fpnc_1200e_icdar2015_20220825_221614-7c0e94f2.pth"
    checkpoint_name = "dbnet_resnet18_fpnc_1200e_icdar2015.pth"
    checkpoint_path = download_checkpoint(checkpoint_dir, checkpoint_url, checkpoint_name)

    # Build model
    model_path = build_dbnet_model(mmdeploy_dir, mmocr_dir, venv_dir, checkpoint_path, work_dir)

    # Download calibration dataset
    calibration_dataset_dir = download_dataset("icdar2015", datasets_dir)

    # Copy model to final location
    final_model_path = models_dir / "dbnet_resnet18_fpnc_1200e_icdar2015_fp32.onnx"
    shutil.copy2(model_path, final_model_path)

    # Quantize model to INT8
    int8_model_path = quantize_dbnet(final_model_path, calibration_dataset_dir, max_images=100)

    elapsed_time = time.time() - start_time
    info(f"DBNet models built successfully in {format_duration(elapsed_time)}")
    info(f"FP32 Model: {final_model_path}")
    info(f"INT8 Model: {int8_model_path}")
    info(f"Calibration data: {calibration_dataset_dir}")


if __name__ == "__main__":
    try:
        build_dbnet()
    except ScriptError as e:
        error(f"Fatal: {e}")
