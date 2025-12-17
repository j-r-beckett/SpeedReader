#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11,<3.13"
# dependencies = [
#     "torch>=2.8.0,<2.9",
#     "transformers>=4.57.0",
#     "accelerate",
#     "qwen-vl-utils>=0.0.14",
#     "pillow",
#     "click",
#     "flash-attn>=2.8.0",
# ]
#
# [tool.uv]
# find-links = ["https://github.com/Dao-AILab/flash-attention/releases/expanded_assets/v2.8.3"]
# ///
"""Zoom into a region of an image based on a natural language description or saved bbox."""

import json
import re
import sys
from datetime import datetime
from pathlib import Path

import click
import torch
from PIL import Image


SCRIPT_DIR = Path(__file__).parent.resolve()
SKILL_DIR = SCRIPT_DIR.parent
OUTPUT_DIR = SKILL_DIR / "output"


def load_model(size: str = "4b"):
    """Load Qwen3-VL model of specified size."""
    from transformers import AutoModelForImageTextToText, AutoProcessor

    model_name = f"Qwen/Qwen3-VL-{size.upper()}-Instruct"

    # Reduced pixel count for faster inference
    # Original: 1280 * 32 * 32 = 1.3M pixels (~35s)
    # 256 patches: 256 * 32 * 32 = 262K pixels (~512x512, ~18s)
    min_pixels = 256 * 32 * 32
    max_pixels = 256 * 32 * 32

    print(f"Loading {model_name}...", file=sys.stderr)
    model = AutoModelForImageTextToText.from_pretrained(
        model_name,
        torch_dtype=torch.bfloat16,
        device_map="auto",
        attn_implementation="sdpa",  # Auto-selects flash_attn if installed, else fallback
    )
    processor = AutoProcessor.from_pretrained(model_name)
    processor.image_processor.min_pixels = min_pixels
    processor.image_processor.max_pixels = max_pixels

    return model, processor


def detect_bbox(model, processor, image: Image.Image, description: str) -> list[int] | None:
    """Get bounding box using Qwen3-VL."""
    orig_w, orig_h = image.size

    prompt = f'Locate {description}, output its bbox coordinates in JSON format like this: {{"bbox_2d": [x1, y1, x2, y2]}}'

    messages = [{
        "role": "user",
        "content": [
            {"type": "image", "image": image},
            {"type": "text", "text": prompt},
        ],
    }]

    inputs = processor.apply_chat_template(
        messages,
        tokenize=True,
        add_generation_prompt=True,
        return_dict=True,
        return_tensors="pt",
    ).to(model.device)

    generated_ids = model.generate(**inputs, max_new_tokens=64)
    output_ids = generated_ids[:, inputs.input_ids.shape[1]:]
    result = processor.batch_decode(output_ids, skip_special_tokens=True)[0]

    print(f"Model response: {result}", file=sys.stderr)

    json_match = re.search(r'\{[^}]+\}', result)
    if not json_match:
        return None

    try:
        data = json.loads(json_match.group())
        bbox = data.get("bbox_2d")
        if bbox and len(bbox) == 4:
            # Qwen3-VL returns coordinates in 0-1000 relative scale
            return [
                max(0, min(int(bbox[0] / 1000 * orig_w), orig_w)),
                max(0, min(int(bbox[1] / 1000 * orig_h), orig_h)),
                max(0, min(int(bbox[2] / 1000 * orig_w), orig_w)),
                max(0, min(int(bbox[3] / 1000 * orig_h), orig_h)),
            ]
    except json.JSONDecodeError:
        pass

    return None


def crop_image(image: Image.Image, bbox: list[int], padding: int = 10) -> Image.Image | None:
    """Crop image to bounding box with optional padding."""
    x1, y1, x2, y2 = bbox
    w, h = image.size

    # Swap if coordinates are reversed
    if x2 < x1:
        x1, x2 = x2, x1
    if y2 < y1:
        y1, y2 = y2, y1

    # Validate bbox has non-zero area
    if x2 <= x1 or y2 <= y1:
        return None

    # Add padding
    x1 = max(0, x1 - padding)
    y1 = max(0, y1 - padding)
    x2 = min(w, x2 + padding)
    y2 = min(h, y2 + padding)

    return image.crop((x1, y1, x2, y2))


@click.command()
@click.argument("image_path", type=click.Path(exists=True))
@click.argument("description_or_bbox")
@click.option("--model", default="4b", type=click.Choice(["4b", "8b", "32b"]), help="Qwen3-VL model size")
def main(image_path: str, description_or_bbox: str, model: str):
    """Zoom into a region of IMAGE_PATH.

    DESCRIPTION_OR_BBOX can be either:
    - A natural language description (uses Qwen3-VL to detect, saves .bbox.json)
    - A path to a .bbox.json file from a previous detection (fast, no model needed)

    Prints the path to the cropped image on stdout.
    """
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    image = Image.open(image_path).convert("RGB")
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")

    # Check if second arg is a .bbox.json file
    if description_or_bbox.endswith(".bbox.json") and Path(description_or_bbox).exists():
        # Load bbox from file
        bbox_path = Path(description_or_bbox)
        with open(bbox_path) as f:
            bbox_data = json.load(f)
        bbox = bbox_data["bbox"]
        print(f"Using saved bbox: {bbox}", file=sys.stderr)
        # No new bbox file needed
        save_bbox = False
    else:
        # Use model to detect bbox
        description = description_or_bbox
        print(f"Finding: {description}", file=sys.stderr)

        qwen_model, processor = load_model(model)
        bbox = detect_bbox(qwen_model, processor, image, description)

        if bbox is None:
            print(f"Error: Could not locate '{description}' in the image", file=sys.stderr)
            sys.exit(1)

        save_bbox = True

    cropped = crop_image(image, bbox)

    if cropped is None:
        print(f"Error: Invalid bounding box {bbox}", file=sys.stderr)
        sys.exit(1)

    # Save crop
    output_path = OUTPUT_DIR / f"crop_{timestamp}.png"
    cropped.save(output_path)

    # Save bbox for reuse (only when detecting, not when reusing)
    if save_bbox:
        bbox_output_path = OUTPUT_DIR / f"crop_{timestamp}.bbox.json"
        with open(bbox_output_path, "w") as f:
            json.dump({"bbox": bbox, "description": description}, f, indent=2)
        print(f"Saved bbox to: {bbox_output_path}", file=sys.stderr)

    print(output_path)


if __name__ == "__main__":
    main()
