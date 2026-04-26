#!/usr/bin/env -S uvx marimo edit --sandbox --no-token --no-skew-protection --watch --port 3006
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

__generated_with = "0.23.3"
app = marimo.App(width="medium")

with app.setup:
    import marimo as mo
    import numpy as np
    import pandas as pd
    import seaborn as sns
    import matplotlib.pyplot as plt
    from pathlib import Path
    import ctypes
    import subprocess
    import threading
    import time
    from notebook_utils import format_duration, prioritized_cores

    sns.set_theme()

    PUBLISH_DIR = Path(__file__).parent / "../src/BenchLib/bin/Release/net10.0/linux-x64/publish"
    LIB_PATH = PUBLISH_DIR / "BenchLib.so"


@app.cell
def _():
    def _():
        with mo.status.spinner(title="Building BenchLib...", remove_on_exit=True):
            result = subprocess.run(
                [
                    "dotnet", "publish", "src/BenchLib",
                    "-p:NativeLib=Shared",
                    "-p:OnnxLinkMode=Dynamic",
                    "--use-current-runtime",
                ],
                cwd=Path(__file__).parent / "..",
                capture_output=True,
                text=True,
            )
            if result.returncode != 0:
                raise RuntimeError(f"Build failed:\n{result.stderr}")

        return mo.md("**BenchLib built successfully.**")

    _()
    return


@app.cell
def _():
    def _():
        lib = ctypes.CDLL(str(LIB_PATH))
        lib.benchlib_init.argtypes = []
        lib.benchlib_init.restype = None
        lib.benchlib_rundbnet.argtypes = [ctypes.c_int]
        lib.benchlib_rundbnet.restype = None
        lib.benchlib_destroy.argtypes = []
        lib.benchlib_destroy.restype = None

        with mo.status.spinner(title="Initializing inference kernel...", remove_on_exit=True):
            lib.benchlib_init()

        return lib

    lib = _()
    return (lib,)


@app.cell
def _(lib):
    def _():
        duration = 8
        max_cores = 8
        core_configs = prioritized_cores(max_cores)

        def worker(lib, core_id, run_duration):
            results = []
            deadline = time.monotonic() + run_duration
            while time.monotonic() < deadline:
                start = time.monotonic()
                lib.benchlib_rundbnet(core_id)
                end = time.monotonic()
                results.append((core_id, start, end))
            return results

        rows = []
        estimated_total = len(core_configs) * (duration + 1)
        start_time_estimate = time.time()

        with mo.status.spinner(title="Running benchmark...", remove_on_exit=True) as spinner:
            for i in range(8):
                cores = [0, 2, 4, 6, 8, 10, 12, 14][:i]
                spinner.update(
                    subtitle=f"Parallelism {len(cores)}: cores {cores} | "
                    f"{format_duration(time.time() - start_time_estimate)} / {format_duration(estimated_total)}"
                )

                thread_results = [[] for _ in cores]
                threads = []
                for i, core_id in enumerate(cores):
                    t = threading.Thread(
                        target=lambda res, cid: res.extend(worker(lib, cid, duration)),
                        args=(thread_results[i], core_id),
                    )
                    threads.append(t)

                for t in threads:
                    t.start()
                for t in threads:
                    t.join()

                for thread_res in thread_results:
                    for core_id, start, end in thread_res:
                        rows.append({
                            "cores": cores,
                            "core_id": core_id,
                            "start_mono": start,
                            "end_mono": end,
                            "duration_ms": (end - start) * 1000,
                        })

        df = pd.DataFrame(rows)
        df["parallelism"] = df["cores"].apply(len)

        # Trim first and last second per parallelism level
        trimmed = []
        for parallelism, g in df.groupby("parallelism"):
            run_start = g["start_mono"].min()
            run_end = g["end_mono"].max()
            midpoints = g["start_mono"] + (g["end_mono"] - g["start_mono"]) / 2
            mask = (midpoints >= run_start + 1) & (midpoints <= run_end - 1)
            trimmed.append(g[mask])

        df = pd.concat(trimmed, ignore_index=True)
        return df

    df = _()
    return (df,)


@app.cell
def _(df):
    def _():
        # Resample to 1-second bins per parallelism level
        rows = []
        for parallelism, g in df.groupby("parallelism"):
            run_start = g["start_mono"].min()
            for _, row in g.iterrows():
                midpoint = row["start_mono"] + (row["end_mono"] - row["start_mono"]) / 2
                rows.append({
                    "parallelism": parallelism,
                    "time_s": int(midpoint - run_start),
                    "duration_ms": row["duration_ms"],
                })

        bin_df = pd.DataFrame(rows)
        throughput_df = (
            bin_df.groupby(["parallelism", "time_s"])
            .size()
            .reset_index(name="inferences_per_sec")
        )

        # Filter to common time range
        max_common_time = throughput_df.groupby("parallelism")["time_s"].max().min()
        throughput_df = throughput_df[throughput_df["time_s"] <= max_common_time]
        return throughput_df

    throughput_df = _()
    return (throughput_df,)


@app.cell
def _(df):
    def _():
        stats_df = df.groupby("parallelism").apply(
            lambda g: pd.Series({
                "avg_throughput": round(
                    len(g) / (g["end_mono"].max() - g["start_mono"].min()), 2
                ),
                "avg_latency_ms": round(g["duration_ms"].mean(), 2),
                "p50_latency_ms": round(g["duration_ms"].median(), 2),
                "p99_latency_ms": round(g["duration_ms"].quantile(0.99), 2),
            }),
            include_groups=False,
        ).reset_index()

        baseline_throughput = stats_df["avg_throughput"].iloc[0]
        stats_df["throughput_marginal_eff"] = (
            stats_df["avg_throughput"].diff() / baseline_throughput
        ).round(3)

        return stats_df

    stats_df = _()
    mo.ui.table(stats_df, page_size=64, show_column_summaries=False)
    return (stats_df,)


@app.cell
def _(throughput_df):
    def _():
        fig, ax = plt.subplots(figsize=(12, 4))
        sns.lineplot(
            data=throughput_df,
            x="time_s",
            y="inferences_per_sec",
            hue="parallelism",
            ax=ax,
            palette="tab10",
        )
        ax.set_xlabel("Time (s)")
        ax.set_ylabel("Inferences / second")
        ax.set_title("Throughput Over Time by Parallelism")
        ax.set_ylim(bottom=0)
        handles, labels = ax.get_legend_handles_labels()
        ax.legend(handles[::-1], labels[::-1], title="Parallelism", loc="upper left", bbox_to_anchor=(1, 1))
        plt.tight_layout()
        return fig

    _()
    return


@app.cell
def _(stats_df):
    def _():
        fig, ax1 = plt.subplots(figsize=(10, 6))

        color1 = "#1a1a2e"
        ax1.plot(
            stats_df["parallelism"],
            stats_df["avg_throughput"],
            marker="o",
            color=color1,
            label="Throughput",
        )
        ax1.set_xlabel("Parallelism")
        ax1.set_ylabel("Avg Inferences / second", color=color1)
        ax1.tick_params(axis="y", labelcolor=color1)
        ax1.set_ylim(bottom=0)

        ax2 = ax1.twinx()
        color2 = "#e63946"
        ax2.plot(
            stats_df["parallelism"],
            stats_df["avg_latency_ms"],
            marker="s",
            color=color2,
            label="Latency",
        )
        ax2.set_ylabel("Avg Latency (ms)", color=color2)
        ax2.tick_params(axis="y", labelcolor=color2)
        ax2.set_ylim(bottom=0)

        ax1.set_title("Throughput and Latency by Parallelism")

        lines1, labels1 = ax1.get_legend_handles_labels()
        lines2, labels2 = ax2.get_legend_handles_labels()
        ax1.legend(lines1 + lines2, labels1 + labels2, loc="lower right")

        plt.tight_layout()
        return fig

    _()
    return


@app.cell
def _(df):
    def _():
        fig, ax = plt.subplots(figsize=(12, 4))
        sns.boxplot(
            data=df,
            x="parallelism",
            y="duration_ms",
            ax=ax,
            color="#1a1a2e",
            fliersize=1,
        )
        ax.set_xlabel("Parallelism")
        ax.set_ylabel("Latency (ms)")
        ax.set_title("Latency Distribution by Parallelism")
        ax.set_ylim(bottom=0)
        plt.tight_layout()
        return fig

    _()
    return


@app.cell
def _(df):
    def _():
        df["core_type"] = df["core_id"].apply(lambda c: "P-core" if c < 16 else "E-core")

        rows = []
        for parallelism, g in df.groupby("parallelism"):
            for core_type, cg in g.groupby("core_type"):
                rows.append({
                    "parallelism": parallelism,
                    "core_type": core_type,
                    "mean_ms": cg["duration_ms"].mean(),
                    "std_ms": cg["duration_ms"].std(),
                    "count": len(cg),
                })

        core_type_df = pd.DataFrame(rows)

        fig, ax1 = plt.subplots(figsize=(10, 5))

        p_core = core_type_df[core_type_df["core_type"] == "P-core"].set_index("parallelism")
        e_core = core_type_df[core_type_df["core_type"] == "E-core"].set_index("parallelism")

        ax1.plot(p_core.index, p_core["mean_ms"], marker="o", color="#1a1a2e", label="P-core Mean", linewidth=2)
        if not e_core.empty:
            ax1.plot(e_core.index, e_core["mean_ms"], marker="o", color="#e63946", label="E-core Mean", linewidth=2)
        ax1.set_xlabel("Parallelism")
        ax1.set_ylabel("Mean Latency (ms)")
        ax1.set_title("P-core vs E-core Mean Latency")
        ax1.set_ylim(bottom=0)
        ax1.set_xticks(core_type_df["parallelism"].unique())
        ax1.legend()
        plt.tight_layout()
        return fig

    _()
    return


if __name__ == "__main__":
    app.run()
