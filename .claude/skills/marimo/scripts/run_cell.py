#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["requests", "websockets"]
# ///
"""Run a marimo cell by name and wait for completion."""

import asyncio
import base64
import json
import re
import sys
import time
from dataclasses import dataclass
from pathlib import Path


import requests
import websockets


@dataclass
class CellOutput:
    """Captured output from a cell."""
    cell_idx: int
    output_type: str  # "dataframe", "figure", "markdown", "html", "text"
    data: bytes | str
    extension: str


def get_session_id(port: int) -> str:
    """Get the session ID from a running marimo server."""
    r = requests.post(f"http://localhost:{port}/api/home/running_notebooks")
    r.raise_for_status()
    files = r.json()["files"]
    if not files:
        raise RuntimeError(f"No notebooks running on port {port}")
    return files[0]["sessionId"]


def parse_output(mimetype: str, data: str, cell_idx: int) -> CellOutput | None:
    """Parse cell output into a CellOutput object."""
    if not data:
        return None

    # DataFrame: text/html with <marimo-table data-data="...">
    if mimetype == "text/html" and "<marimo-table" in data:
        match = re.search(r'data-data=[\'"]([^\'"]+)[\'"]', data)
        if match:
            # Unescape HTML entities
            json_str = match.group(1)
            json_str = json_str.replace("&quot;", '"').replace("&#92;", "\\")
            try:
                # Parse and re-serialize for pretty printing
                parsed = json.loads(json_str)
                return CellOutput(
                    cell_idx=cell_idx,
                    output_type="dataframe",
                    data=json.dumps(parsed, indent=2),
                    extension="json"
                )
            except json.JSONDecodeError:
                pass

    # Figure: application/vnd.marimo+mimebundle with image/png
    if mimetype == "application/vnd.marimo+mimebundle":
        try:
            bundle = json.loads(data)
            if "image/png" in bundle:
                png_data = bundle["image/png"]
                # Remove data URL prefix if present
                if png_data.startswith("data:image/png;base64,"):
                    png_data = png_data[len("data:image/png;base64,"):]
                return CellOutput(
                    cell_idx=cell_idx,
                    output_type="figure",
                    data=base64.b64decode(png_data),
                    extension="png"
                )
            if "image/svg+xml" in bundle:
                return CellOutput(
                    cell_idx=cell_idx,
                    output_type="figure",
                    data=bundle["image/svg+xml"],
                    extension="svg"
                )
        except (json.JSONDecodeError, KeyError):
            pass

    # Markdown: text/markdown
    if mimetype == "text/markdown":
        return CellOutput(
            cell_idx=cell_idx,
            output_type="markdown",
            data=data,
            extension="html"  # It's rendered HTML
        )

    # Plain HTML (simple values like integers, strings)
    if mimetype == "text/html":
        # Extract text from <pre> tags for simple values
        match = re.search(r"<pre[^>]*>([^<]+)</pre>", data)
        if match:
            return CellOutput(
                cell_idx=cell_idx,
                output_type="text",
                data=match.group(1),
                extension="txt"
            )

    return None


async def run_cell_and_wait(
    port: int, session_id: str, cell_name: str
) -> tuple[list[int], dict[int, str], list[str], dict[int, CellOutput]]:
    """Run a cell and wait for it (and dependents) to complete.

    Returns (cells_ran, console_outputs, errors, cell_outputs) where:
      - cells_ran: list of cell indices that executed
      - console_outputs: dict mapping cell index to stdout/stderr output
      - errors: list of error messages
      - cell_outputs: dict mapping cell index to CellOutput
    """
    uri = f"ws://localhost:{port}/ws?session_id={session_id}&kiosk=true"
    async with websockets.connect(uri, close_timeout=0.1) as ws:
        # Get cell data from kernel-ready message
        msg = json.loads(await ws.recv())
        if msg.get("op") != "kernel-ready":
            raise RuntimeError(f"Unexpected message: {msg.get('op')}")

        data = msg["data"]
        names = data["names"]
        cell_ids = data["cell_ids"]
        codes = data["codes"]

        name_to_id = {n: cid for n, cid in zip(names, cell_ids)}
        id_to_code = dict(zip(cell_ids, codes))

        # Sync kernel state with file changes (must be on same websocket connection).
        # When --watch detects changes, the server knows the new code but the
        # kernel's internal execution state may be stale. Calling save syncs them.
        nb_resp = requests.post(f"http://localhost:{port}/api/home/running_notebooks")
        filename = nb_resp.json()["files"][0]["path"]
        configs = [{"disabled": False, "hide_code": False} for _ in cell_ids]
        requests.post(
            f"http://localhost:{port}/api/kernel/save",
            headers={"Marimo-Session-Id": session_id},
            json={
                "cellIds": cell_ids,
                "codes": codes,
                "names": names,
                "configs": configs,
                "filename": filename,
                "persist": False,
            },
        )
        await asyncio.sleep(0.2)  # Give kernel time to process

        # Skip the setup cell (first cell) for user-facing indices
        # cell_1 = first @app.cell, not the setup cell
        user_cell_ids = cell_ids[1:]  # Exclude setup cell
        id_to_index = {cid: i + 1 for i, cid in enumerate(user_cell_ids)}

        # Check if cell_name is an index like "cell_1"
        index_match = re.match(r'^cell_(\d+)$', cell_name)
        if index_match:
            idx = int(index_match.group(1)) - 1  # Convert to 0-based
            if 0 <= idx < len(user_cell_ids):
                cell_id = user_cell_ids[idx]
            else:
                raise RuntimeError(
                    f"Cell index {idx + 1} out of range. Notebook has {len(user_cell_ids)} cells (excluding setup)."
                )
        elif cell_name in name_to_id:
            cell_id = name_to_id[cell_name]
        else:
            available = [n for n in names if n != "_"]
            indexed = [f"cell_{i+1}" for i in range(len(user_cell_ids))]
            raise RuntimeError(
                f"Cell '{cell_name}' not found.\n"
                f"Named cells: {available}\n"
                f"By index: {indexed}"
            )

        code = id_to_code[cell_id]

        # Trigger the run
        r = requests.post(
            f"http://localhost:{port}/api/kernel/run",
            headers={"Marimo-Session-Id": session_id},
            json={"cellIds": [cell_id], "codes": [code]},
        )
        r.raise_for_status()
        if not r.json().get("success"):
            raise RuntimeError(f"Failed to run cell: {r.json()}")

        # Collect errors, console output, cell outputs, and track which cells ran
        errors = []
        seen_errors = set()
        cells_ran = []
        cells_running = set()  # Cells that have started running (status=running seen)
        console_outputs = {}  # cell_idx -> collected output
        cell_outputs = {}  # cell_idx -> CellOutput

        while True:
            msg = json.loads(await ws.recv())
            op = msg.get("op")

            if op == "cell-op":
                cell_op_data = msg.get("data", {})
                cell_id_affected = cell_op_data.get("cell_id")
                status = cell_op_data.get("status")
                cell_idx = id_to_index.get(cell_id_affected)

                # Track cells that actually ran (status transitions to "running")
                # IMPORTANT: Only collect console/output AFTER seeing status=running,
                # because marimo sends cached output before the cell actually runs.
                if status == "running" and cell_id_affected not in cells_running:
                    cells_running.add(cell_id_affected)
                    if cell_idx:
                        cells_ran.append(cell_idx)

                # Skip cached state (before cell starts running) - it has stale data
                if cell_id_affected not in cells_running:
                    continue

                # Collect console output only from messages after cell started running
                console = cell_op_data.get("console") or []
                for console_item in console:
                    if not isinstance(console_item, dict):
                        continue
                    channel = console_item.get("channel")
                    if channel in ("stdout", "stderr"):
                        text = console_item.get("data", "")
                        if text and cell_idx:
                            if cell_idx not in console_outputs:
                                console_outputs[cell_idx] = ""
                            console_outputs[cell_idx] += text

                output = cell_op_data.get("output") or {}
                mimetype = output.get("mimetype", "")
                output_data = output.get("data", "")

                if cell_idx and mimetype and output_data:
                    # Check for error first
                    if mimetype == "application/vnd.marimo+error":
                        error_data = output.get("data", [])

                        # Extract traceback from console output
                        traceback_text = ""
                        for console_item in console:
                            if console_item.get("mimetype") == "application/vnd.marimo+traceback":
                                html_traceback = console_item.get("data", "")
                                traceback_text = re.sub(r'<[^>]+>', '', html_traceback)

                        # Separate main exception from detail entries
                        main_error = None
                        detail_entries = []
                        for err in error_data:
                            if err.get("type") == "exception" or err.get("exception_type"):
                                main_error = err
                            elif err.get("name") and err.get("cells"):
                                detail_entries.append(err)

                        if main_error:
                            err_type = main_error.get("exception_type") or main_error.get("type", "Error")
                            err_msg = main_error.get("msg", "Unknown error")
                        elif detail_entries:
                            first_detail = detail_entries[0]
                            err_type = first_detail.get("type", "Error")
                            var_name = first_detail.get("name", "")
                            err_msg = f"This cell redefines '{var_name}' from another cell"
                        else:
                            err_type = "Error"
                            err_msg = "Unknown error"

                        err_key = (cell_id_affected, err_type)
                        if err_key not in seen_errors:
                            seen_errors.add(err_key)
                            error_parts = [f"cell_{cell_idx}: {err_type}: {err_msg}"]
                            for detail in detail_entries:
                                var_name = detail.get("name", "")
                                def_cells = detail.get("cells", [])
                                if var_name and def_cells:
                                    cell_nums = [f"cell_{id_to_index.get(c, c)}" for c in def_cells]
                                    error_parts.append(f"  '{var_name}' was also defined by: {', '.join(cell_nums)}")
                            if traceback_text:
                                error_parts.append(traceback_text)
                            errors.append("\n".join(error_parts))
                    else:
                        # Parse non-error output
                        parsed = parse_output(mimetype, output_data, cell_idx)
                        if parsed:
                            cell_outputs[cell_idx] = parsed

            elif op == "completed-run":
                break

    # Reconnect to get fresh console output from updated cache
    # Marimo doesn't stream console during execution - it's only in cached state
    # Small delay to ensure cache is fully updated
    await asyncio.sleep(0.1)

    if cells_running:
        seen_cells = set()
        async with websockets.connect(uri, close_timeout=0.1) as ws:
            msg = json.loads(await ws.recv())
            if msg.get("op") == "kernel-ready":
                # Collect cell-op messages with fresh cached state
                while len(seen_cells) < len(cells_running):
                    try:
                        msg = json.loads(await asyncio.wait_for(ws.recv(), timeout=2.0))
                    except asyncio.TimeoutError:
                        break

                    if msg.get("op") == "cell-op":
                        cell_data = msg.get("data", {})
                        cell_id = cell_data.get("cell_id")
                        cell_idx = id_to_index.get(cell_id)
                        console = cell_data.get("console")

                        if cell_id in cells_running:
                            seen_cells.add(cell_id)
                            if cell_idx and console:
                                for item in console:
                                    if isinstance(item, dict) and item.get("channel") in ("stdout", "stderr"):
                                        text = item.get("data", "")
                                        if text:
                                            if cell_idx not in console_outputs:
                                                console_outputs[cell_idx] = ""
                                            console_outputs[cell_idx] += text

    return sorted(cells_ran), console_outputs, errors, cell_outputs


def save_outputs(output_dir: Path, cell_outputs: dict[int, CellOutput]) -> dict[int, str]:
    """Save cell outputs to disk. Returns dict of cell_idx -> saved filepath."""
    output_dir.mkdir(parents=True, exist_ok=True)
    saved = {}

    for cell_idx, output in cell_outputs.items():
        filename = f"cell_{cell_idx}_{output.output_type}.{output.extension}"
        filepath = output_dir / filename

        if isinstance(output.data, bytes):
            filepath.write_bytes(output.data)
        else:
            filepath.write_text(output.data)

        saved[cell_idx] = str(filepath)

    return saved


def main(cell_name: str, port: int, output_dir: str | None = None) -> None:
    # Brief wait for --watch to detect file changes
    time.sleep(0.1)
    session_id = get_session_id(port)
    cells_ran, console_outputs, errors, cell_outputs = asyncio.run(
        run_cell_and_wait(port, session_id, cell_name)
    )

    # Format which cells ran
    cells_str = ", ".join(f"cell_{i}" for i in cells_ran) if cells_ran else cell_name

    if errors:
        print(f"Ran [{cells_str}] with errors:", file=sys.stderr)
        for err in errors:
            print(err, file=sys.stderr)
        sys.exit(1)
    else:
        print(f"Ran [{cells_str}]")

    # Print console output if any
    for cell_idx in sorted(console_outputs.keys()):
        output = console_outputs[cell_idx].strip()
        if output:
            print(f"cell_{cell_idx} output:\n{output}")

    # Save and report captured outputs
    if output_dir:
        saved = save_outputs(Path(output_dir), cell_outputs)
        if saved:
            print(f"\nCaptured outputs:")
            for cell_idx in sorted(saved.keys()):
                filepath = saved[cell_idx]
                output_type = cell_outputs[cell_idx].output_type
                print(f"  cell_{cell_idx} [{output_type}]: {filepath}")
    elif cell_outputs:
        # Report what was captured even without saving
        print(f"\nCaptured (not saved, pass output_dir to save):")
        for cell_idx in sorted(cell_outputs.keys()):
            output_type = cell_outputs[cell_idx].output_type
            print(f"  cell_{cell_idx} [{output_type}]")


if __name__ == "__main__":
    if len(sys.argv) < 3 or len(sys.argv) > 4:
        print(f"Usage: {sys.argv[0]} <cell_id> <port> [output_dir]", file=sys.stderr)
        sys.exit(1)

    cell_name = sys.argv[1]
    port = int(sys.argv[2])
    output_dir = sys.argv[3] if len(sys.argv) > 3 else None
    main(cell_name, port, output_dir)
