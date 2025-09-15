#!/usr/bin/env python3
# Copyright (c) 2025 j-r-beckett
# Licensed under the Apache License, Version 2.0

import sys
import subprocess
import os
from pathlib import Path
import argparse
from typing import List
import numpy as np
from PIL import Image
import onnxruntime
from onnxruntime.quantization import quantize_static, CalibrationDataReader, QuantType

def check_docker():
    """Check if Docker daemon is running"""
    try:
        result = subprocess.run(
            ["docker", "info"],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            timeout=5
        )
        return result.returncode == 0
    except (subprocess.TimeoutExpired, FileNotFoundError):
        return False

class CalibrationDataset(CalibrationDataReader):
    """Calibration dataset reader for DBNet quantization"""

    def __init__(self, calibration_dir: str, max_images: int = None):
        self.calibration_dir = Path(calibration_dir)
        self.image_files = list(self.calibration_dir.glob("*.jpg")) + list(self.calibration_dir.glob("*.png"))

        if max_images:
            self.image_files = self.image_files[:max_images]

        print(f"Using {len(self.image_files)} calibration images")
        self.current_index = 0

    def get_next(self):
        if self.current_index >= len(self.image_files):
            return None

        image_path = self.image_files[self.current_index]
        self.current_index += 1

        # Load and preprocess image for DBNet
        image = Image.open(image_path).convert('RGB')
        # Resize to standard input size (adjust based on your model's requirements)
        image = image.resize((640, 640))

        # Convert to numpy array and normalize
        image_array = np.array(image, dtype=np.float32)
        image_array = image_array.transpose(2, 0, 1)  # HWC to CHW
        image_array = image_array / 255.0  # Normalize to [0, 1]

        # Add batch dimension
        image_array = np.expand_dims(image_array, axis=0)

        return {'input': image_array}

def simplify_onnx_models(models_dir):
    """Simplify all ONNX models in the models directory"""
    models_path = Path(models_dir)
    onnx_files = list(models_path.rglob("*.onnx"))

    if not onnx_files:
        return

    print(f"Simplifying {len(onnx_files)} ONNX models...")

    for onnx_file in onnx_files:
        # Skip already quantized models
        if "_int8" in onnx_file.name:
            continue

        print(f"Simplifying {onnx_file.name}")

        backup_file = onnx_file.with_suffix('.onnx.orig')
        onnx_file.rename(backup_file)

        try:
            subprocess.run([
                "python", "-m", "onnxsim",
                str(backup_file),
                str(onnx_file)
            ], check=True)
            backup_file.unlink()
        except subprocess.CalledProcessError:
            backup_file.rename(onnx_file)

def quantize_models(models_dir, calibration_dir, max_calibration_images=None):
    """Quantize ONNX models to INT8 using calibration data"""
    models_path = Path(models_dir)

    # Find DBNet model
    dbnet_model = models_path / "dbnet_resnet18_fpnc_1200e_icdar2015" / "end2end.onnx"

    if dbnet_model.exists():
        print(f"Quantizing DBNet model with {max_calibration_images or 'all'} calibration images...")

        # Create calibration data reader
        calibration_data_reader = CalibrationDataset(calibration_dir, max_calibration_images)

        # Output path for quantized model
        quantized_model = dbnet_model.with_name("end2end_int8.onnx")

        try:
            quantize_static(
                model_input=str(dbnet_model),
                model_output=str(quantized_model),
                calibration_data_reader=calibration_data_reader,
                quant_format=onnxruntime.quantization.QuantFormat.QDQ,
                weight_type=QuantType.QInt8,
                activation_type=QuantType.QUInt8
            )
            print(f"Created quantized model: {quantized_model}")
        except Exception as e:
            print(f"Warning: Quantization failed: {e}")
    else:
        print("Warning: DBNet model not found for quantization")

def extract_calibration_data(container_id, output_dir):
    """Extract calibration dataset from Docker container"""
    calibration_path = Path(output_dir)
    calibration_path.mkdir(exist_ok=True)

    print("Extracting calibration dataset...")
    subprocess.run([
        "docker", "cp",
        f"{container_id}:/calibration_data/.",
        str(calibration_path)
    ], check=True)

    # Count extracted files
    image_files = list(calibration_path.glob("*.jpg")) + list(calibration_path.glob("*.png"))
    print(f"Extracted {len(image_files)} calibration images")

    return str(calibration_path)

def main():
    parser = argparse.ArgumentParser(description='Build and quantize ONNX models')
    parser.add_argument('output_dir', help='Output directory for models')
    parser.add_argument('--max-calibration-images', type=int, default=100,
                       help='Maximum number of calibration images to use (default: 100, 0 for all)')

    args = parser.parse_args()

    if args.max_calibration_images == 0:
        max_calibration_images = None
    else:
        max_calibration_images = args.max_calibration_images

    print("Building models...")

    if not check_docker():
        print("Error: Docker daemon is not running or not responding")
        sys.exit(1)

    out_dir = args.output_dir
    calibration_dir = os.path.join(out_dir, "calibration_data")

    # Build Docker image
    print("Building Docker image...")
    subprocess.run([
        "docker", "build", "-t", "modelbuilder:latest", "--progress", "plain", "."
    ], check=True)

    # Create container
    result = subprocess.run([
        "docker", "create", "modelbuilder:latest"
    ], capture_output=True, text=True, check=True)
    container_id = result.stdout.strip()

    try:
        # Start container
        subprocess.run([
            "docker", "start", container_id
        ], stdout=subprocess.DEVNULL, check=True)

        # Create output directory
        Path(out_dir).mkdir(parents=True, exist_ok=True)

        # Copy models from container
        print("Extracting models...")
        subprocess.run([
            "docker", "cp", f"{container_id}:/models/.", f"{out_dir}/"
        ], check=True)

        # Extract calibration data
        extract_calibration_data(container_id, calibration_dir)

        # Simplify the ONNX models (before quantization)
        simplify_onnx_models(out_dir)

        # Quantize models
        quantize_models(out_dir, calibration_dir, max_calibration_images)

        print(f"Models built, simplified, and quantized successfully in {out_dir}")

    finally:
        # Clean up container
        subprocess.run([
            "docker", "rm", container_id
        ], stdout=subprocess.DEVNULL, check=True)

if __name__ == "__main__":
    main()
