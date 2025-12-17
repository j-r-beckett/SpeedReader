#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = [
#     "torch",
#     "torchvision",
#     "transformers>=4.49.0",
#     "accelerate",
#     "qwen-vl-utils",
#     "pillow",
#     "click",
# ]
# ///
"""Zoom into a region of an image based on a natural language description."""

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


def get_resized_dimensions(image: Image.Image, processor) -> tuple[int, int]:
    """Get the dimensions the processor will resize the image to."""
    from qwen_vl_utils.vision_process import smart_resize

    w, h = image.size
    min_pixels = getattr(processor.image_processor, 'min_pixels', 256 * 28 * 28)
    max_pixels = getattr(processor.image_processor, 'max_pixels', 1280 * 28 * 28)

    resized_h, resized_w = smart_resize(h, w, factor=28, min_pixels=min_pixels, max_pixels=max_pixels)
    return resized_w, resized_h


def load_model():
    """Load Qwen2.5-VL-3B model."""
    from transformers import Qwen2_5_VLForConditionalGeneration, Qwen2_5_VLProcessor

    model_name = "Qwen/Qwen2.5-VL-3B-Instruct"

    # Limit pixel count to fit in 8GB VRAM
    # 1280 * 28 * 28 ≈ 1M pixels max
    min_pixels = 256 * 28 * 28
    max_pixels = 1280 * 28 * 28

    model = Qwen2_5_VLForConditionalGeneration.from_pretrained(
        model_name,
        torch_dtype=torch.bfloat16,
        device_map="auto",
    )
    processor = Qwen2_5_VLProcessor.from_pretrained(
        model_name,
        min_pixels=min_pixels,
        max_pixels=max_pixels,
    )

    return model, processor


def get_bounding_box(model, processor, image: Image.Image, description: str) -> list[int] | None:
    """Get bounding box for the described element."""
    from qwen_vl_utils import process_vision_info

    # Get the dimensions the processor will use
    resized_w, resized_h = get_resized_dimensions(image, processor)
    orig_w, orig_h = image.size

    prompt = f'Locate {description}, output its bbox coordinates in JSON format like this: {{"bbox_2d": [x1, y1, x2, y2]}}'

    messages = [{
        "role": "user",
        "content": [
            {
                "type": "image",
                "image": image,
            },
            {"type": "text", "text": prompt},
        ],
    }]

    text = processor.apply_chat_template(messages, tokenize=False, add_generation_prompt=True)
    image_inputs, video_inputs = process_vision_info(messages)
    inputs = processor(
        text=[text],
        images=image_inputs,
        videos=video_inputs,
        padding=True,
        return_tensors="pt",
    ).to(model.device)

    generated_ids = model.generate(**inputs, max_new_tokens=256)
    output_ids = generated_ids[:, inputs.input_ids.shape[1]:]
    result = processor.batch_decode(output_ids, skip_special_tokens=True)[0]

    # Parse JSON from response (may be wrapped in markdown code blocks)
    json_match = re.search(r'\{[^}]+\}', result)
    if not json_match:
        return None

    try:
        data = json.loads(json_match.group())
        bbox = data.get("bbox_2d")
        if bbox and len(bbox) == 4:
            # Model returns coordinates relative to resized image
            # Scale back to original image coordinates
            scale_x = orig_w / resized_w
            scale_y = orig_h / resized_h

            return [
                max(0, min(int(bbox[0] * scale_x), orig_w)),
                max(0, min(int(bbox[1] * scale_y), orig_h)),
                max(0, min(int(bbox[2] * scale_x), orig_w)),
                max(0, min(int(bbox[3] * scale_y), orig_h)),
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
@click.argument("description")
@click.option("--padding", default=10, help="Padding around the detected region in pixels")
def main(image_path: str, description: str, padding: int):
    """Zoom into a region of IMAGE_PATH matching DESCRIPTION.

    Prints the path to the cropped image on stdout.
    """
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    # Load image
    image = Image.open(image_path).convert("RGB")

    # Load model (prints to stderr so stdout stays clean)
    print("Loading model...", file=sys.stderr)
    model, processor = load_model()

    # Get bounding box
    print(f"Finding: {description}", file=sys.stderr)
    bbox = get_bounding_box(model, processor, image, description)

    if bbox is None:
        print(f"Error: Could not locate '{description}' in the image", file=sys.stderr)
        sys.exit(1)

    # Crop and save
    cropped = crop_image(image, bbox, padding=padding)

    if cropped is None:
        print(f"Error: Invalid bounding box {bbox} for '{description}'", file=sys.stderr)
        sys.exit(1)

    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    output_path = OUTPUT_DIR / f"crop_{timestamp}.png"
    cropped.save(output_path)

    # Print only the path to stdout
    print(output_path)


if __name__ == "__main__":
    main()
