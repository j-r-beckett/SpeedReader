#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "build_utils"]
#
# [tool.uv.sources]
# build_utils = { path = "../build_utils", editable = true }
# ///

import json
import shutil
import subprocess
import webbrowser
from dataclasses import dataclass
from pathlib import Path

import click

from build_utils import ScriptError, bash, error, info

SCRIPT_DIR = Path(__file__).parent.resolve()
SRC_DIR = SCRIPT_DIR.parent / "src"
RESULTS_DIR = SRC_DIR / "TestResults"
COVERAGE_JSON = RESULTS_DIR / "coverage.json"
COVERAGE_XML = RESULTS_DIR / "coverage.cobertura.xml"
REPORT_DIR = RESULTS_DIR / "CoverageReport"

TEST_PROJECTS = ["Resources.Test", "Native.Test", "Ocr.Test", "Frontend.Test"]

COVERLET_INCLUDE = "[Ocr]*,[Resources]*,[speedreader]*,[Native]*"
COVERLET_EXCLUDE = "[*.Test]*,[TestUtils]*,[Benchmarks]*"
COVERLET_EXCLUDE_FILES = "**/obj/**/*"


@dataclass
class CoverageResult:
    line: float
    branch: float
    report_path: Path


def run_test_project(
    project: str, is_first: bool, is_last: bool, deflake: bool, max_failures: int
) -> int:
    """Run a test project with coverage. Returns number of test failures encountered."""
    output_format = "cobertura" if is_last else "json"
    output_file = COVERAGE_XML if is_last else COVERAGE_JSON

    base_args = [
        "dotnet",
        "test",
        project,
        "/p:CollectCoverage=true",
        f"/p:CoverletOutputFormat={output_format}",
        f"/p:CoverletOutput=../TestResults/{output_file.name}",
        f'/p:Include="{COVERLET_INCLUDE}"',
        f'/p:Exclude="{COVERLET_EXCLUDE}"',
        f'/p:ExcludeByFile="{COVERLET_EXCLUDE_FILES}"',
    ]

    if not is_first:
        base_args.append(f"/p:MergeWith=../TestResults/{COVERAGE_JSON.name}")

    failures = 0
    while True:
        result = subprocess.run(
            base_args,
            cwd=SRC_DIR,
            capture_output=True,
            text=True,
        )

        if result.returncode == 0:
            return failures

        failures += 1

        if not deflake:
            # Continue without deflaking - coverage still collected for passing tests
            return failures

        if failures >= max_failures:
            raise ScriptError(
                f"Exceeded {max_failures} test failures while deflaking. Aborting."
            )

        info(f"{project} failed (attempt {failures}), retrying...")


def parse_coverage_summary(xml_path: Path) -> CoverageResult:
    """Parse coverage percentages from cobertura XML."""
    import xml.etree.ElementTree as ET

    tree = ET.parse(xml_path)
    root = tree.getroot()

    line_rate = float(root.get("line-rate", 0)) * 100
    branch_rate = float(root.get("branch-rate", 0)) * 100

    return CoverageResult(
        line=line_rate,
        branch=branch_rate,
        report_path=REPORT_DIR / "index.html",
    )


def generate_report() -> CoverageResult:
    """Generate HTML report using reportgenerator."""
    bash(
        f'reportgenerator -reports:"{COVERAGE_XML}" '
        f'-targetdir:"{REPORT_DIR}" '
        f"-reporttypes:Html",
        directory=SRC_DIR,
    )

    return parse_coverage_summary(COVERAGE_XML)


@click.command()
@click.option("--json", "json_output", is_flag=True, help="Output results as JSON")
@click.option("--open", "open_browser", is_flag=True, help="Open coverage report in browser")
@click.option("--deflake", is_flag=True, help="Retry failed tests until they pass")
def main(json_output: bool, open_browser: bool, deflake: bool):
    """Run tests and generate coverage report."""
    # Clean previous results
    if RESULTS_DIR.exists():
        shutil.rmtree(RESULTS_DIR)
    RESULTS_DIR.mkdir(parents=True)

    # Build first
    info("Building solution...")
    bash("dotnet build", directory=SRC_DIR)

    # Run tests with coverage
    total_failures = 0
    max_failures = 10

    for i, project in enumerate(TEST_PROJECTS):
        is_first = i == 0
        is_last = i == len(TEST_PROJECTS) - 1

        if not json_output:
            info(f"Running {project}...")

        failures = run_test_project(
            project,
            is_first=is_first,
            is_last=is_last,
            deflake=deflake,
            max_failures=max_failures - total_failures,
        )
        total_failures += failures

    # Generate report
    if not json_output:
        info("Generating coverage report...")

    result = generate_report()

    # Output results
    if json_output:
        output = {
            "line_coverage": round(result.line, 2),
            "branch_coverage": round(result.branch, 2),
            "report_path": str(result.report_path),
        }
        print(json.dumps(output))
    else:
        info(f"Line coverage:   {result.line:.1f}%")
        info(f"Branch coverage: {result.branch:.1f}%")
        info(f"Report: {result.report_path}")

    if open_browser:
        webbrowser.open(f"file://{result.report_path}")


if __name__ == "__main__":
    try:
        main()
    except ScriptError as e:
        error(f"Fatal: {e}")
        exit(1)
