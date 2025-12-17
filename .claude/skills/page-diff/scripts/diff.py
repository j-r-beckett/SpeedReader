#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "pillow", "pixelmatch"]
# ///

"""Compare two images and generate a visual diff overlay."""

from datetime import datetime
from pathlib import Path

import click
from PIL import Image
from pixelmatch import pixelmatch
from pixelmatch.contrib.PIL import pixelmatch as pixelmatch_pil

SCRIPT_DIR = Path(__file__).parent.resolve()
OUTPUT_DIR = SCRIPT_DIR.parent / "output"


def create_overlay(actual: Image.Image, diff: Image.Image, alpha: float = 0.5) -> Image.Image:
    """Composite diff onto actual image with transparency."""
    # Convert both to RGBA
    actual_rgba = actual.convert("RGBA")
    diff_rgba = diff.convert("RGBA")

    # Create overlay: where diff is magenta (changed pixels), blend with actual
    # Where diff is black/transparent (unchanged), show actual
    overlay = actual_rgba.copy()

    for x in range(diff_rgba.width):
        for y in range(diff_rgba.height):
            diff_pixel = diff_rgba.getpixel((x, y))
            # Magenta pixels indicate differences (R > 200, B > 200, G < 100)
            if diff_pixel[0] > 200 and diff_pixel[2] > 200 and diff_pixel[1] < 100:
                actual_pixel = actual_rgba.getpixel((x, y))
                # Blend: tint the actual pixel with magenta
                blended = (
                    int(actual_pixel[0] * (1 - alpha) + 255 * alpha),
                    int(actual_pixel[1] * (1 - alpha) + 0 * alpha),
                    int(actual_pixel[2] * (1 - alpha) + 255 * alpha),
                    255
                )
                overlay.putpixel((x, y), blended)

    return overlay


def create_overlay_fast(actual: Image.Image, diff: Image.Image, alpha: float = 0.5) -> Image.Image:
    """Fast overlay using PIL blend (less precise but much faster)."""
    actual_rgba = actual.convert("RGBA")
    diff_rgba = diff.convert("RGBA")

    # Create a magenta tint layer from the diff
    # Pixelmatch outputs red (255,0,0) for diff pixels
    tint = Image.new("RGBA", actual_rgba.size, (255, 0, 255, 0))

    diff_data = diff_rgba.load()
    tint_data = tint.load()

    for x in range(diff_rgba.width):
        for y in range(diff_rgba.height):
            pixel = diff_data[x, y]
            # Check for red diff pixels (pixelmatch default: 255,0,0)
            if pixel[0] > 200 and pixel[1] < 100 and pixel[2] < 100:
                tint_data[x, y] = (255, 0, 255, int(255 * alpha))

    # Composite tint over actual
    return Image.alpha_composite(actual_rgba, tint)


@click.command()
@click.argument("expected", type=click.Path(exists=True, path_type=Path))
@click.argument("actual", type=click.Path(exists=True, path_type=Path))
@click.option("--threshold", "-t", default=0.1, help="Color difference threshold (0-1). Higher = more tolerant.")
@click.option("--alpha", "-a", default=0.5, help="Overlay transparency (0-1).")
@click.option("--output", "-o", type=click.Path(path_type=Path), help="Output path for overlay image.")
def main(expected: Path, actual: Path, threshold: float, alpha: float, output: Path | None):
    """
    Compare EXPECTED and ACTUAL images, generate diff overlay.

    Outputs similarity percentage and saves overlay image showing differences
    highlighted in magenta.
    """
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    # Load images
    img_expected = Image.open(expected).convert("RGBA")
    img_actual = Image.open(actual).convert("RGBA")

    # Check dimensions match
    if img_expected.size != img_actual.size:
        click.echo(f"ERROR: Image dimensions don't match", err=True)
        click.echo(f"  Expected: {img_expected.size[0]}x{img_expected.size[1]}", err=True)
        click.echo(f"  Actual:   {img_actual.size[0]}x{img_actual.size[1]}", err=True)
        raise SystemExit(1)

    width, height = img_expected.size
    total_pixels = width * height

    # Create diff image
    img_diff = Image.new("RGBA", (width, height))

    # Run pixelmatch
    diff_pixels = pixelmatch_pil(
        img_expected,
        img_actual,
        img_diff,
        threshold=threshold,
        alpha=0.1,
        includeAA=True
    )

    # Calculate similarity
    similarity = ((total_pixels - diff_pixels) / total_pixels) * 100

    # Generate output path
    if output is None:
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        output = OUTPUT_DIR / f"diff_{timestamp}.png"

    # Create and save overlay
    overlay = create_overlay_fast(img_actual, img_diff, alpha)
    overlay.save(output)

    # Output results
    if diff_pixels == 0:
        click.echo(f"PASS: 100% identical")
    elif similarity >= 99:
        click.echo(f"PASS: {similarity:.1f}% similar ({diff_pixels:,} pixels differ)")
    else:
        click.echo(f"DIFF: {similarity:.1f}% similar ({diff_pixels:,} pixels differ)")

    click.echo(str(output))


if __name__ == "__main__":
    main()
