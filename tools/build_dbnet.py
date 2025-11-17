#!/usr/bin/env -S uv run

import os
import time
import shutil
import urllib.request
from pathlib import Path
from utils import ScriptError, bash, info, error, format_duration
from download_dataset import download_dataset


def checkout_repo(repo_dir: Path, repo_url: str, version: str, repo_name: str):
    """Clone or checkout the correct version of a git repository"""
    if not (repo_dir / "README.md").exists():
        repo_dir.mkdir(parents=True, exist_ok=True)
        info(f"Cloning {repo_name} {version} to {repo_dir}")
        bash(
            f"git clone --depth 1 --branch {version} {repo_url} {repo_dir}"
        )
        if not (repo_dir / "README.md").exists():
            raise ScriptError(f"Failed to clone {repo_name}")
        info(f"Successfully cloned {repo_name}")
    else:
        info(f"{repo_name} already cloned at {repo_dir}")


def create_mmdeploy_venv(build_dir: Path) -> Path:
    """Create and configure Python environment for mmdeploy"""
    venv_dir = build_dir / ".venv"

    if not venv_dir.exists():
        info("Creating mmdeploy python environment")
        # Use Python 3.10 (mmcv 2.0.1 only supports Python 3.7-3.10, not 3.11+)
        bash(f"uv venv {venv_dir} --python 3.10", directory=build_dir)

    info("Installing mmdeploy dependencies")
    # Install pip in venv (needed for mmcv installation from OpenMMLab index)
    bash(
        f"uv pip install --python {venv_dir}/bin/python pip",
        directory=build_dir,
    )
    # Install dependencies matching the Dockerfile
    # Pin numpy<2.0 for compatibility with torch 2.0.0
    bash(
        f"uv pip install --python {venv_dir}/bin/python "
        f"'numpy<2.0' torch==2.0.0 torchvision==0.15.0 --index-url https://download.pytorch.org/whl/cpu",
        directory=build_dir,
    )
    bash(
        f"uv pip install --python {venv_dir}/bin/python "
        f"mmengine==0.7.4",
        directory=build_dir,
    )
    # Install mmcv from OpenMMLab's pre-built wheel repository
    # PyPI doesn't have wheels for our specific torch/python combination
    # The wheel includes pre-compiled C++ extensions
    # Use pip with --find-links (same as Dockerfile) since it's a simple HTML page, not a PyPI index
    bash(
        f"{venv_dir}/bin/pip install "
        f"mmcv==2.0.1 -f https://download.openmmlab.com/mmcv/dist/cpu/torch2.0/index.html",
        directory=build_dir,
    )
    bash(
        f"uv pip install --python {venv_dir}/bin/python "
        f"mmdet==3.0.0 mmocr==1.0.1 mmdeploy==1.3.1 onnxruntime==1.15.0",
        directory=build_dir,
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


def build_dbnet():
    """Build DBNet FP32 model end-to-end"""
    start_time = time.time()

    # Setup directories
    repo_root = Path(__file__).parent.parent.resolve()
    build_dir = repo_root / "target" / "models" / "build"
    models_dir = repo_root / "models"
    datasets_dir = repo_root / "datasets"

    mmdeploy_dir = build_dir / "mmdeploy"
    mmocr_dir = build_dir / "mmocr"
    checkpoint_dir = build_dir / "checkpoints"
    work_dir = build_dir / "dbnet_work"

    # Clone repositories
    checkout_repo(
        mmdeploy_dir,
        "https://github.com/open-mmlab/mmdeploy",
        "v1.3.1",
        "mmdeploy"
    )
    checkout_repo(
        mmocr_dir,
        "https://github.com/open-mmlab/mmocr",
        "v1.0.1",
        "mmocr"
    )

    # Create Python environment
    venv_dir = create_mmdeploy_venv(build_dir)

    # Download checkpoint
    checkpoint_url = "https://download.openmmlab.com/mmocr/textdet/dbnet/dbnet_resnet18_fpnc_1200e_icdar2015/dbnet_resnet18_fpnc_1200e_icdar2015_20220825_221614-7c0e94f2.pth"
    checkpoint_name = "dbnet_resnet18_fpnc_1200e_icdar2015.pth"
    checkpoint_path = download_checkpoint(checkpoint_dir, checkpoint_url, checkpoint_name)

    # Build model
    model_path = build_dbnet_model(mmdeploy_dir, mmocr_dir, venv_dir, checkpoint_path, work_dir)

    # Download calibration dataset
    download_dataset("icdar2015", datasets_dir)

    # Copy model to final location
    models_dir.mkdir(parents=True, exist_ok=True)
    final_model_path = models_dir / "dbnet_resnet18_fpnc_1200e_icdar2015_fp32.onnx"
    shutil.copy2(model_path, final_model_path)

    elapsed_time = time.time() - start_time
    info(f"DBNet FP32 model built successfully in {format_duration(elapsed_time)}")
    info(f"Model: {final_model_path}")
    info(f"Calibration data: {datasets_dir / 'icdar2015'}")


if __name__ == "__main__":
    try:
        build_dbnet()
    except ScriptError as e:
        error(f"Fatal: {e}")
