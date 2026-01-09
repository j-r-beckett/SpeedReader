#!/usr/bin/env -S uvx marimo edit --sandbox --no-token --no-skew-protection --watch --port 3009
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
    from notebook_utils import format_duration, run_benchmark, start_perf

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
        # Full system benchmark: shift P-cores from SVTR to DbNet
        # Config: N P-cores for DbNet, remaining P-cores + all E-cores for SVTR
        configs = []
        for dbnet_p_count in range(1, len(P_CORES) + 1):
            dbnet_cores = P_CORES[:dbnet_p_count]
            svtr_cores = P_CORES[dbnet_p_count:] + E_CORES
            configs.append((dbnet_cores, svtr_cores))

        estimated_total = len(configs) * (WARMUP + DURATION + 1)
        start_time_estimate = time.time()

        rows = []
        with mo.status.spinner(title="Running full system benchmark...", remove_on_exit=True) as spinner:
            spinner.update(subtitle=f"0s / {format_duration(estimated_total)}")
            perf = start_perf()
            for dbnet_cores, svtr_cores in configs:
                cmd = [
                    "dotnet", "run", str(SCRIPT), "--",
                    "--dbnet-cores", *[str(c) for c in dbnet_cores],
                    "--svtr-cores", *[str(c) for c in svtr_cores],
                ]
                for start_time, end_time, tags in run_benchmark(cmd, DURATION, WARMUP):
                    elapsed = time.time() - start_time_estimate
                    spinner.update(subtitle=f"{format_duration(elapsed)} / {format_duration(estimated_total)}")
                    rows.append({
                        "dbnet_cores": len(dbnet_cores),
                        "model": tags.get("model", "unknown"),
                        "start_time": start_time,
                        "end_time": end_time,
                    })
            perf_df = perf.stop()

        df = pd.DataFrame(rows)

        # Compute stats: throughput by (dbnet_cores, model)
        stats_rows = []
        for (dbnet_cores, model), g in df.groupby(["dbnet_cores", "model"]):
            total_duration_s = (g["end_time"].max() - g["start_time"].min()).total_seconds()
            stats_rows.append({
                "dbnet_cores": dbnet_cores,
                "model": model,
                "throughput": len(g) / total_duration_s,
            })

        stats_df = pd.DataFrame(stats_rows).sort_values(["dbnet_cores", "model"])
        return df, perf_df, stats_df

    df, perf_df, full_system_stats = _()
    return df, full_system_stats, perf_df


@app.cell
def _(df, full_system_stats, perf_df):
    def _():
        fig, (ax1, ax3) = plt.subplots(2, 1, figsize=(10, 8))

        # Top plot: DbNet vs SVTR throughput (dual y-axis)
        dbnet_stats = full_system_stats[full_system_stats["model"] == "dbnet"]
        svtr_stats = full_system_stats[full_system_stats["model"] == "svtr"]

        ax1.plot(
            dbnet_stats["dbnet_cores"],
            dbnet_stats["throughput"],
            marker="o",
            linewidth=2,
            label="DbNet",
            color="#3498db",
        )
        ax1.set_xlabel("DbNet P-core Count")
        ax1.set_ylabel("DbNet Throughput (inferences/sec)", color="#3498db")
        ax1.tick_params(axis="y", labelcolor="#3498db")
        ax1.set_ylim(bottom=0)
        ax1.set_xticks(range(1, 9))
        ax1.grid(True, alpha=0.3)

        ax2 = ax1.twinx()
        ax2.plot(
            svtr_stats["dbnet_cores"],
            svtr_stats["throughput"],
            marker="o",
            linewidth=2,
            label="SVTR",
            color="#2ecc71",
        )
        ax2.set_ylabel("SVTR Throughput (inferences/sec)", color="#2ecc71")
        ax2.tick_params(axis="y", labelcolor="#2ecc71")
        ax2.set_ylim(bottom=0)

        lines1, labels1 = ax1.get_legend_handles_labels()
        lines2, labels2 = ax2.get_legend_handles_labels()
        ax1.legend(lines1 + lines2, labels1 + labels2, loc="center right")
        ax1.set_title("Full System: DbNet vs SVTR Throughput by P-core Allocation")

        # Bottom plot: DRAM bandwidth vs DbNet P-core count
        if not perf_df.empty:
            bw_rows = []
            for dbnet_cores, g in df.groupby("dbnet_cores"):
                t_start = g["start_time"].min()
                t_end = g["end_time"].max()
                mask = (perf_df["timestamp"] >= t_start) & (perf_df["timestamp"] <= t_end)
                run_perf = perf_df[mask]
                if not run_perf.empty:
                    bw_rows.append({
                        "dbnet_cores": dbnet_cores,
                        "bandwidth_gbps": run_perf["bandwidth_gbps"].mean(),
                    })

            if bw_rows:
                bw_df = pd.DataFrame(bw_rows).sort_values("dbnet_cores")
                ax3.plot(
                    bw_df["dbnet_cores"],
                    bw_df["bandwidth_gbps"],
                    marker="o",
                    linewidth=2,
                    color="#e74c3c",
                )
                ax3.set_xlabel("DbNet P-core Count")
                ax3.set_ylabel("DRAM Bandwidth (GB/s)")
                ax3.set_title("Memory Bandwidth by DbNet P-core Allocation")
                ax3.set_ylim(bottom=0)
                ax3.set_xticks(range(1, 9))
                ax3.grid(True, alpha=0.3)

        plt.tight_layout()
        return fig

    _()
    return


if __name__ == "__main__":
    app.run()
