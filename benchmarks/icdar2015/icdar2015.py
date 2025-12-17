#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "tqdm", "Polygon3", "numpy"]
# ///

import json
import math
import os
import re
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path

import click
from tqdm import tqdm

SCRIPT_DIR = Path(__file__).parent.resolve()
REPO_ROOT = SCRIPT_DIR.parent.parent
TEST_DIR = SCRIPT_DIR / "test"
GT_ZIP = Path.home() / "library" / "icdar2015" / "gt.zip"


def rotated_rect_to_corners(rect: dict) -> list[tuple[float, float]]:
    """Convert rotatedRectangle {x, y, width, height, angle} to 4 corner points.

    Returns points in clockwise order: top-left, top-right, bottom-right, bottom-left.
    This matches the C# RotatedRectangle.Corners() implementation.
    """
    x, y = rect["x"], rect["y"]
    width, height = rect["width"], rect["height"]
    angle = rect["angle"]

    cos_a = math.cos(angle)
    sin_a = math.sin(angle)

    top_left = (x, y)
    top_right = (x + width * cos_a, y + width * sin_a)
    bottom_right = (x + width * cos_a - height * sin_a, y + width * sin_a + height * cos_a)
    bottom_left = (x - height * sin_a, y + height * cos_a)

    return [top_left, top_right, bottom_right, bottom_left]


def corners_to_icdar_line(corners: list[tuple[float, float]]) -> str:
    """Convert 4 corner points to ICDAR format: x1,y1,x2,y2,x3,y3,x4,y4"""
    coords = []
    for x, y in corners:
        coords.extend([int(round(x)), int(round(y))])
    return ",".join(map(str, coords))


def extract_image_number(filename: str) -> int | None:
    """Extract image number from filename like 'img_123.jpg' or full path."""
    basename = Path(filename).stem
    match = re.match(r"img_(\d+)", basename)
    return int(match.group(1)) if match else None


def run_speedreader(image_paths: list[Path], output_file: Path) -> None:
    """Run SpeedReader on images and write JSON Lines output to file."""
    cmd = ["dotnet", "run", "--project", str(REPO_ROOT / "src" / "Frontend"), "--"]
    cmd.extend(str(p) for p in image_paths)

    with open(output_file, "w") as f:
        process = subprocess.Popen(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            cwd=REPO_ROOT
        )

        # Read stdout line by line, write JSON Lines
        buffer = ""
        results_written = 0
        pbar = tqdm(total=len(image_paths), desc="Running OCR", unit="img")

        for line in process.stdout:
            line = line.strip()
            if not line:
                continue

            # Skip JSON array delimiters
            if line in ("[", "]", ","):
                continue

            # Remove leading comma if present
            if line.startswith(","):
                line = line[1:].strip()

            # Try to parse as JSON
            buffer += line
            try:
                obj = json.loads(buffer)
                f.write(json.dumps(obj) + "\n")
                f.flush()
                buffer = ""
                results_written += 1
                pbar.update(1)
            except json.JSONDecodeError:
                # Incomplete JSON, keep buffering
                continue

        pbar.close()
        process.wait()

        if process.returncode != 0:
            stderr = process.stderr.read()
            raise RuntimeError(f"SpeedReader failed: {stderr}")


def parse_results(jsonl_file: Path) -> dict[int, list[dict]]:
    """Parse JSON Lines file and return dict mapping image number to detections."""
    results = {}

    with open(jsonl_file) as f:
        for line in f:
            line = line.strip()
            if not line:
                continue

            obj = json.loads(line)
            img_num = extract_image_number(obj["filename"])
            if img_num is None:
                continue

            detections = []
            for result in obj.get("results", []):
                bbox = result.get("boundingBox", {})
                rotated_rect = bbox.get("rotatedRectangle")
                if rotated_rect:
                    corners = rotated_rect_to_corners(rotated_rect)
                    detections.append({
                        "corners": corners,
                        "confidence": result.get("confidence", 0.0)
                    })

            results[img_num] = detections

    return results


def create_submission_zip(results: dict[int, list[dict]], output_path: Path) -> None:
    """Create ICDAR submission zip file."""
    with zipfile.ZipFile(output_path, "w") as zf:
        for img_num in range(1, 501):
            detections = results.get(img_num, [])
            lines = []
            for det in detections:
                line = corners_to_icdar_line(det["corners"])
                lines.append(line)

            content = "\n".join(lines)
            zf.writestr(f"res_img_{img_num}.txt", content)


def run_evaluation(gt_path: Path, submission_path: Path) -> dict:
    """Run ICDAR evaluation and return results."""
    # Import evaluation module from same directory
    sys.path.insert(0, str(SCRIPT_DIR))
    import evaluation

    eval_params = evaluation.default_evaluation_params()

    # Validate and evaluate
    evaluation.validate_data(str(gt_path), str(submission_path), eval_params)
    results = evaluation.evaluate_method(str(gt_path), str(submission_path), eval_params)

    return results


@click.command()
@click.option("--test-dir", type=click.Path(exists=True, path_type=Path), default=TEST_DIR,
              help="Directory containing test images")
@click.option("--gt", type=click.Path(exists=True, path_type=Path), default=GT_ZIP,
              help="Path to ground truth zip file")
@click.option("--output", type=click.Path(path_type=Path), default=None,
              help="Output directory for results (default: temp dir)")
@click.option("--keep-output", is_flag=True, help="Keep output files after evaluation")
def main(test_dir: Path, gt: Path, output: Path | None, keep_output: bool):
    """Run ICDAR 2015 text detection benchmark on SpeedReader."""

    # Find test images
    image_paths = sorted(test_dir.glob("img_*.jpg"), key=lambda p: extract_image_number(p.name) or 0)
    if not image_paths:
        raise click.ClickException(f"No test images found in {test_dir}")

    click.echo(f"Found {len(image_paths)} test images")

    # Set up output directory
    if output is None:
        output = Path(tempfile.mkdtemp(prefix="icdar2015_"))
    output.mkdir(parents=True, exist_ok=True)

    jsonl_file = output / "results.jsonl"
    submission_zip = output / "submit.zip"

    try:
        # Run SpeedReader
        click.echo("Running SpeedReader...")
        run_speedreader(image_paths, jsonl_file)

        # Parse results
        click.echo("Parsing results...")
        results = parse_results(jsonl_file)
        click.echo(f"Parsed results for {len(results)} images")

        # Create submission
        click.echo("Creating submission zip...")
        create_submission_zip(results, submission_zip)

        # Run evaluation
        click.echo("Running evaluation...")
        eval_results = run_evaluation(gt, submission_zip)

        # Print results
        method = eval_results.get("method", {})
        click.echo("\n" + "=" * 50)
        click.echo("ICDAR 2015 Text Detection Results")
        click.echo("=" * 50)
        click.echo(f"Precision: {method.get('precision', 0):.4f}")
        click.echo(f"Recall:    {method.get('recall', 0):.4f}")
        click.echo(f"H-mean:    {method.get('hmean', 0):.4f}")
        click.echo("=" * 50)

        if keep_output:
            click.echo(f"\nOutput files saved to: {output}")

    finally:
        if not keep_output and output.exists():
            import shutil
            shutil.rmtree(output, ignore_errors=True)


if __name__ == "__main__":
    main()
