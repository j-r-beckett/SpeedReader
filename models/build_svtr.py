#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["utils"]
#
# [tool.uv.sources]
# utils = { path = "../tools/utils", editable = true }
# ///

import shutil
import time
import urllib.request
from pathlib import Path
from utils import ScriptError, bash, info, error, format_duration


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


def create_openocr_venv(build_dir: Path, openocr_dir: Path) -> Path:
    """Create and configure Python environment for OpenOCR"""
    venv_dir = build_dir / ".venv_openocr"

    if not venv_dir.exists():
        info("Creating OpenOCR python environment")
        bash(f"uv venv {venv_dir} --python 3.10", directory=build_dir)

    info("Installing OpenOCR dependencies")
    # Install CPU-only PyTorch
    bash(
        f"uv pip install --python {venv_dir}/bin/python "
        f"'numpy<2.0' torch==2.0.0 torchvision==0.15.0 --index-url https://download.pytorch.org/whl/cpu",
        directory=build_dir,
    )
    # Install OpenOCR requirements
    bash(
        f"uv pip install --python {venv_dir}/bin/python "
        f"-r {openocr_dir}/requirements.txt",
        directory=build_dir,
    )
    # Install onnx for export
    bash(
        f"uv pip install --python {venv_dir}/bin/python onnx",
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


def patch_openocr_export_bug(openocr_dir: Path):
    """Patch a bug in OpenOCR's export_rec.py (line 71 uses cfg instead of _cfg)"""
    export_rec_path = openocr_dir / "tools" / "export_rec.py"
    content = export_rec_path.read_text()

    # Fix line 71: cfg['Architecture'] -> _cfg['Architecture']
    buggy_line = "    cfg['Architecture']['Decoder']['out_channels'] = char_num"
    fixed_line = "    _cfg['Architecture']['Decoder']['out_channels'] = char_num"

    if buggy_line in content:
        info("Patching OpenOCR export_rec.py bug")
        content = content.replace(buggy_line, fixed_line)
        export_rec_path.write_text(content)


def build_svtr_model(openocr_dir: Path, venv_dir: Path, checkpoint_path: Path, work_dir: Path):
    """Build SVTRv2 ONNX model using OpenOCR's export tool"""
    info("Building SVTRv2 FP32 model")

    # Patch OpenOCR bug before export
    patch_openocr_export_bug(openocr_dir)

    # Clean work directory
    if work_dir.exists():
        shutil.rmtree(work_dir)
    work_dir.mkdir(parents=True, exist_ok=True)

    # Run OpenOCR's export_rec.py tool
    # Export config: shape [batch, channels, height, width], dynamic batch axis
    char_dict_path = openocr_dir / "tools" / "utils" / "ppocr_keys_v1.txt"
    bash(
        f"source {venv_dir}/bin/activate && "
        f"python3 {openocr_dir}/tools/export_rec.py "
        f"--config {openocr_dir}/configs/rec/svtrv2/repsvtr_ch.yml "
        f"--type onnx "
        f"-o Global.device=cpu "
        f"Global.pretrained_model={checkpoint_path} "
        f"Global.character_dict_path={char_dict_path} "
        f"PostProcess.character_dict_path={char_dict_path} "
        f"PostProcess.use_space_char=True "
        f"Export.export_shape=[1,3,48,320] "
        f"'Export.dynamic_axes=[0,3]' "
        f"Export.export_dir={work_dir}",
        directory=openocr_dir,
    )

    # Check if model was created
    model_path = work_dir / "model.onnx"
    if not model_path.exists():
        raise ScriptError(f"Model not found at {model_path}")

    return model_path


def build_svtr():
    """Build SVTRv2 FP32 model end-to-end"""
    start_time = time.time()

    # Setup directories
    repo_root = Path(__file__).parent.parent.resolve()
    build_dir = repo_root / "target" / "models" / "build"
    models_dir = repo_root / "models"

    openocr_dir = build_dir / "OpenOCR"
    checkpoint_dir = build_dir / "checkpoints"
    work_dir = build_dir / "svtr_work"

    # Clone OpenOCR repository
    checkout_repo(
        openocr_dir,
        "https://github.com/Topdu/OpenOCR.git",
        "develop0.0.1",
        "OpenOCR"
    )

    # Create Python environment
    venv_dir = create_openocr_venv(build_dir, openocr_dir)

    # Download checkpoint
    checkpoint_url = "https://github.com/Topdu/OpenOCR/releases/download/develop0.0.1/openocr_repsvtr_ch.pth"
    checkpoint_name = "openocr_repsvtr_ch.pth"
    checkpoint_path = download_checkpoint(checkpoint_dir, checkpoint_url, checkpoint_name)

    # Build model
    model_path = build_svtr_model(openocr_dir, venv_dir, checkpoint_path, work_dir)

    # Copy model to final location
    models_dir.mkdir(parents=True, exist_ok=True)
    final_model_path = models_dir / "svtrv2_base_ctc_fp32.onnx"
    shutil.copy2(model_path, final_model_path)

    elapsed_time = time.time() - start_time
    info(f"SVTRv2 model built successfully in {format_duration(elapsed_time)}")
    info(f"FP32 Model: {final_model_path}")


if __name__ == "__main__":
    try:
        build_svtr()
    except ScriptError as e:
        error(f"Fatal: {e}")
