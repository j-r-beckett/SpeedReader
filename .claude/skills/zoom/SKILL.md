---
name: zoom
description: Crops a region from an image based on a natural language description. Use when you need to zoom in on a UI element, button, text, icon, or any visual feature in a screenshot or image. Helps verify rendering, inspect details, or focus on specific areas.
---

# Zoom

Extracts a cropped region from an image using Qwen3-VL vision-language grounding.

## Requirements

**NVIDIA GPU with 8GB+ VRAM required.** Before first use, check available VRAM:

```bash
nvidia-smi --query-gpu=name,memory.total --format=csv
```

If the user doesn't have an NVIDIA GPU with at least 8GB VRAM, this skill cannot be used.

## Usage

```bash
uv run .claude/skills/zoom/scripts/zoom.py <image> "<description>" [--model 4b|8b|32b]
```

The script prints the path to the cropped image on stdout. Use `Read` to view the result.

**Examples:**
```bash
uv run .claude/skills/zoom/scripts/zoom.py screenshot.png "the Submit button"
uv run .claude/skills/zoom/scripts/zoom.py screenshot.png "the json output window"
uv run .claude/skills/zoom/scripts/zoom.py screenshot.png "the word 'Complexity'"
```

If this tool returns an incorrect result, try tweaking the prompt.

## Model Options

| Model | VRAM | Speed | Notes |
|-------|------|-------|-------|
| `--model 4b` | ~8GB | ~18s | Default, best balance |
| `--model 8b` | ~16GB | - | More capable |
| `--model 32b` | ~64GB | - | Most capable |

## Output

Each detection saves two files to `.claude/skills/zoom/output/`:
- `crop_<timestamp>.png` - the cropped image
- `crop_<timestamp>.bbox.json` - the bounding box coordinates for reuse

## Reusing a Bounding Box

Once you've detected a region, reuse the `.bbox.json` file to crop the same region from other images without running the model:

```bash
# First: detect and save bbox (~18s)
uv run .claude/skills/zoom/scripts/zoom.py screenshot1.png "the Submit button"
# -> output/crop_20241217_120000.png
# -> output/crop_20241217_120000.bbox.json

# Later: apply same bbox to new images (instant)
uv run .claude/skills/zoom/scripts/zoom.py screenshot2.png output/crop_20241217_120000.bbox.json
uv run .claude/skills/zoom/scripts/zoom.py screenshot3.png output/crop_20241217_120000.bbox.json
```

This is useful when cropping the same UI region across multiple screenshots.

## Prompting Tips

**Good prompts include a combination of element type + context + position + features:**
```
"the Submit button"
"the blue button on the right side of the page"
"the blue search icon in the header"
"the dropdown menu labeled 'File'"
"the image in the center of the screen"
```
