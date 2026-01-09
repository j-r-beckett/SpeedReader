#!/usr/bin/env -S uvx marimo edit --sandbox --no-token --no-skew-protection --watch --port 3008
# /// script
# requires-python = ">=3.11"
# dependencies = ["marimo", "numpy", "pandas", "seaborn", "notebook_utils"]
#
# [tool.uv.sources]
# notebook_utils = { path = "notebook_utils", editable = true }
#
# [tool.marimo.runtime]
# auto_reload = "lazy"
# auto_instantiate = false
# ///

import marimo

__generated_with = "0.18.4"
app = marimo.App(width="medium")

with app.setup:
    import marimo as mo
    import numpy as np
    import pandas as pd
    import seaborn as sns
    import matplotlib.pyplot as plt
    from pathlib import Path
    import time
    from notebook_utils import format_duration, run_benchmark

    sns.set_theme()

    # Core topology for i7-14700K
    P_CORES = [0, 2, 4, 6, 8, 10, 12, 14]  # 8 P-cores (physical threads)
    E_CORES = list(range(16, 28))  # 12 E-cores

    SCRIPT = Path(__file__).parent / "combined.script.cs"
    DURATION = 6
    WARMUP = 2


@app.cell
def _():
    def _():
        # 1. SVTR scaling without interference
        e_core_configs = [E_CORES[:n] for n in range(1, len(E_CORES) + 1)]

        estimated_total = len(e_core_configs) * (WARMUP + DURATION + 1)
        start_time_estimate = time.time()

        rows = []
        with mo.status.spinner(title="1/4: SVTR scaling (no interference)...", remove_on_exit=True) as spinner:
            spinner.update(subtitle=f"0s / {format_duration(estimated_total)}")
            for e_cores in e_core_configs:
                cmd = [
                    "dotnet", "run", str(SCRIPT), "--",
                    "--svtr-cores", *[str(c) for c in e_cores],
                ]
                for start_time, end_time, tags in run_benchmark(cmd, DURATION, WARMUP):
                    elapsed = time.time() - start_time_estimate
                    spinner.update(subtitle=f"{format_duration(elapsed)} / {format_duration(estimated_total)}")
                    rows.append({
                        "core_count": len(e_cores),
                        "start_time": start_time,
                        "end_time": end_time,
                    })

        df = pd.DataFrame(rows)

        stats_rows = []
        for core_count, g in df.groupby("core_count"):
            total_duration_s = (g["end_time"].max() - g["start_time"].min()).total_seconds()
            stats_rows.append({
                "core_count": core_count,
                "throughput": len(g) / total_duration_s,
            })

        return pd.DataFrame(stats_rows).sort_values("core_count")

    svtr_alone = _()
    return (svtr_alone,)


@app.cell
def _():
    def _():
        # 2. SVTR scaling with max interference (DbNet on all 8 P-cores)
        e_core_configs = [E_CORES[:n] for n in range(1, len(E_CORES) + 1)]

        estimated_total = len(e_core_configs) * (WARMUP + DURATION + 1)
        start_time_estimate = time.time()

        rows = []
        with mo.status.spinner(title="2/4: SVTR scaling (max DbNet interference)...", remove_on_exit=True) as spinner:
            spinner.update(subtitle=f"0s / {format_duration(estimated_total)}")
            for e_cores in e_core_configs:
                cmd = [
                    "dotnet", "run", str(SCRIPT), "--",
                    "--svtr-cores", *[str(c) for c in e_cores],
                    "--dbnet-cores", *[str(c) for c in P_CORES],
                ]
                for start_time, end_time, tags in run_benchmark(cmd, DURATION, WARMUP):
                    elapsed = time.time() - start_time_estimate
                    spinner.update(subtitle=f"{format_duration(elapsed)} / {format_duration(estimated_total)}")
                    if tags.get("model") == "svtr":
                        rows.append({
                            "core_count": len(e_cores),
                            "start_time": start_time,
                            "end_time": end_time,
                        })

        df = pd.DataFrame(rows)

        stats_rows = []
        for core_count, g in df.groupby("core_count"):
            total_duration_s = (g["end_time"].max() - g["start_time"].min()).total_seconds()
            stats_rows.append({
                "core_count": core_count,
                "throughput": len(g) / total_duration_s,
            })

        return pd.DataFrame(stats_rows).sort_values("core_count")

    svtr_with_dbnet = _()
    return (svtr_with_dbnet,)


@app.cell
def _():
    def _():
        # 3. DbNet scaling without interference
        p_core_configs = [P_CORES[:n] for n in range(1, len(P_CORES) + 1)]

        estimated_total = len(p_core_configs) * (WARMUP + DURATION + 1)
        start_time_estimate = time.time()

        rows = []
        with mo.status.spinner(title="3/4: DbNet scaling (no interference)...", remove_on_exit=True) as spinner:
            spinner.update(subtitle=f"0s / {format_duration(estimated_total)}")
            for p_cores in p_core_configs:
                cmd = [
                    "dotnet", "run", str(SCRIPT), "--",
                    "--dbnet-cores", *[str(c) for c in p_cores],
                ]
                for start_time, end_time, tags in run_benchmark(cmd, DURATION, WARMUP):
                    elapsed = time.time() - start_time_estimate
                    spinner.update(subtitle=f"{format_duration(elapsed)} / {format_duration(estimated_total)}")
                    rows.append({
                        "core_count": len(p_cores),
                        "start_time": start_time,
                        "end_time": end_time,
                    })

        df = pd.DataFrame(rows)

        stats_rows = []
        for core_count, g in df.groupby("core_count"):
            total_duration_s = (g["end_time"].max() - g["start_time"].min()).total_seconds()
            stats_rows.append({
                "core_count": core_count,
                "throughput": len(g) / total_duration_s,
            })

        return pd.DataFrame(stats_rows).sort_values("core_count")

    dbnet_alone = _()
    return (dbnet_alone,)


@app.cell
def _():
    def _():
        # 4. DbNet scaling with max interference (SVTR on all 12 E-cores)
        p_core_configs = [P_CORES[:n] for n in range(1, len(P_CORES) + 1)]

        estimated_total = len(p_core_configs) * (WARMUP + DURATION + 1)
        start_time_estimate = time.time()

        rows = []
        with mo.status.spinner(title="4/4: DbNet scaling (max SVTR interference)...", remove_on_exit=True) as spinner:
            spinner.update(subtitle=f"0s / {format_duration(estimated_total)}")
            for p_cores in p_core_configs:
                cmd = [
                    "dotnet", "run", str(SCRIPT), "--",
                    "--dbnet-cores", *[str(c) for c in p_cores],
                    "--svtr-cores", *[str(c) for c in E_CORES],
                ]
                for start_time, end_time, tags in run_benchmark(cmd, DURATION, WARMUP):
                    elapsed = time.time() - start_time_estimate
                    spinner.update(subtitle=f"{format_duration(elapsed)} / {format_duration(estimated_total)}")
                    if tags.get("model") == "dbnet":
                        rows.append({
                            "core_count": len(p_cores),
                            "start_time": start_time,
                            "end_time": end_time,
                        })

        df = pd.DataFrame(rows)

        stats_rows = []
        for core_count, g in df.groupby("core_count"):
            total_duration_s = (g["end_time"].max() - g["start_time"].min()).total_seconds()
            stats_rows.append({
                "core_count": core_count,
                "throughput": len(g) / total_duration_s,
            })

        return pd.DataFrame(stats_rows).sort_values("core_count")

    dbnet_with_svtr = _()
    return (dbnet_with_svtr,)


@app.cell
def _(svtr_alone, svtr_with_dbnet):
    def _():
        fig, ax = plt.subplots(figsize=(10, 5))

        ax.plot(
            svtr_alone["core_count"],
            svtr_alone["throughput"],
            marker="o",
            linewidth=2,
            label="SVTR alone",
            color="#2ecc71",
        )

        ax.plot(
            svtr_with_dbnet["core_count"],
            svtr_with_dbnet["throughput"],
            marker="o",
            linewidth=2,
            label="SVTR with DbNet (8 P-cores)",
            color="#e74c3c",
        )

        ax.set_xlabel("E-core Count")
        ax.set_ylabel("Throughput (inferences/sec)")
        ax.set_title("SVTR Scaling: Alone vs With Max DbNet Interference")
        ax.set_ylim(bottom=0)
        ax.set_xticks(range(1, 13))
        ax.legend(loc="lower right")
        ax.grid(True, alpha=0.3)

        plt.tight_layout()
        return fig

    _()
    return


@app.cell
def _(dbnet_alone, dbnet_with_svtr):
    def _():
        fig, ax = plt.subplots(figsize=(10, 5))

        ax.plot(
            dbnet_alone["core_count"],
            dbnet_alone["throughput"],
            marker="o",
            linewidth=2,
            label="DbNet alone",
            color="#3498db",
        )

        ax.plot(
            dbnet_with_svtr["core_count"],
            dbnet_with_svtr["throughput"],
            marker="o",
            linewidth=2,
            label="DbNet with SVTR (12 E-cores)",
            color="#e74c3c",
        )

        ax.set_xlabel("P-core Count")
        ax.set_ylabel("Throughput (inferences/sec)")
        ax.set_title("DbNet Scaling: Alone vs With Max SVTR Interference")
        ax.set_ylim(bottom=0)
        ax.set_xticks(range(1, 9))
        ax.legend(loc="lower right")
        ax.grid(True, alpha=0.3)

        plt.tight_layout()
        return fig

    _()
    return


if __name__ == "__main__":
    app.run()
