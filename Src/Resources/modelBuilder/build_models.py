#!/usr/bin/env python3

import sys
import subprocess
import os
from pathlib import Path

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

def simplify_onnx_models(models_dir):
    """Simplify all ONNX models in the models directory"""
    models_path = Path(models_dir)
    onnx_files = list(models_path.rglob("*.onnx"))
    
    if not onnx_files:
        return
    
    print(f"Simplifying {len(onnx_files)} ONNX models...")
    
    for onnx_file in onnx_files:
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

def main():
    if len(sys.argv) != 2:
        print("Error: outDir parameter is required")
        sys.exit(1)
    
    print("Building models...")
    
    if not check_docker():
        print("Error: Docker daemon is not running or not responding")
        sys.exit(1)
    
    out_dir = sys.argv[1]
    
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
        subprocess.run([
            "docker", "cp", f"{container_id}:/models/.", f"{out_dir}/"
        ], check=True)
        
        # Simplify the ONNX models
        simplify_onnx_models(out_dir)
        
        print(f"Models built and simplified successfully in {out_dir}")
        
    finally:
        # Clean up container
        subprocess.run([
            "docker", "rm", container_id
        ], stdout=subprocess.DEVNULL, check=True)

if __name__ == "__main__":
    main()