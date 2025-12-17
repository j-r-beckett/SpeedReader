---
name: zoom
description: Crops a region from an image based on a natural language description. Use when you need to zoom in on a UI element, button, text, icon, or any visual feature in a screenshot or image. Helps verify rendering, inspect details, or focus on specific areas.
---

# Zoom

Extracts a cropped region from an image using vision-language grounding. Powered by Qwen2.5-VL-3B.

## Usage

```bash
uv run .claude/skills/zoom/scripts/zoom.py <image> "<description>"
```

The script prints the path to the cropped image on stdout.

**Examples:**
```bash
uv run .claude/skills/zoom/scripts/zoom.py screenshot.png "the Submit button"
uv run .claude/skills/zoom/scripts/zoom.py page.png "the search icon in the header"
uv run .claude/skills/zoom/scripts/zoom.py form.png "the email input field"
```

## Output

Cropped images are saved to `.claude/skills/zoom/output/` with timestamped filenames. The script prints only the output path to stdout - use `Read` to view the cropped image.

## Prompting Guidelines

**Do include:**
- Element type: "button", "icon", "input field", "link", "menu"
- Visual attributes: "blue", "large", "highlighted"
- Spatial context: "in the top right", "at the bottom", "next to the logo"

**Good examples:**
```bash
"the Submit button"                    # element type
"the blue Save button"                 # type + attribute
"the search icon in the header"        # type + location
"the error message at the top"         # type + location
"the dropdown menu labeled 'File'"     # type + label
```

**Avoid:**
```bash
"Submit"                    # too vague, no element type
"the text"                  # which text?
"the thing on the right"    # no element type
```

**Known limitations:**
- Dense/overlapping elements (>40-50 instances) may cause issues
- Very small elements may be imprecise
- Pure OCR ("the word 'X'") is less reliable than element descriptions

## Notes

- Model: Qwen2.5-VL-3B-Instruct (bf16, ~6GB VRAM)
- First run downloads model (~6GB), subsequent runs take ~3-5s

## Fixes

