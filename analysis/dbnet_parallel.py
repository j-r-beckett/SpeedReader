#!/usr/bin/env -S uv run marimo edit --no-token --no-skew-protection --watch --port 3006

import marimo

__generated_with = "0.23.3"
app = marimo.App(width="medium")

with app.setup:
    import marimo as mo
    import pandas as pd
    import seaborn as sns
    import matplotlib.pyplot as plt
    from bench import build, run_dbnet, start_perf

    sns.set_theme()


@app.cell
def _():
    build()
    return


@app.cell
def _():
    perf = start_perf()
    df = run_dbnet(
        configs=[[j * 2 for j in range(i)] for i in range(1, 8)],
        duration=8,
        trim=1.0,
    )
    cpu_perf, sys_perf = perf.stop()
    return cpu_perf, df, sys_perf


@app.cell
def _(df):
    def _():
        stats = df.groupby("parallelism").apply(
            lambda g: pd.Series({
                "avg_throughput": len(g) / (g["end_mono"].max() - g["start_mono"].min()),
                "avg_latency_ms": g["duration_ms"].mean(),
            }),
            include_groups=False,
        ).reset_index()

        fig, ax1 = plt.subplots(figsize=(10, 6))

        color1 = "#1a1a2e"
        ax1.plot(stats["parallelism"], stats["avg_throughput"], marker="o", color=color1, label="Throughput")
        ax1.set_xlabel("Parallelism")
        ax1.set_ylabel("Avg Inferences / second", color=color1)
        ax1.tick_params(axis="y", labelcolor=color1)
        ax1.set_ylim(bottom=0)

        ax2 = ax1.twinx()
        color2 = "#e63946"
        ax2.plot(stats["parallelism"], stats["avg_latency_ms"], marker="s", color=color2, label="Latency")
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
def _(sys_perf):
    def _():
        if sys_perf.empty:
            return mo.md("No system perf data")

        fig, ax = plt.subplots(figsize=(10, 4))
        ax.plot(sys_perf["interval"], sys_perf["bandwidth_gbps"], marker=".", linewidth=1.5)
        ax.set_xlabel("Time (s)")
        ax.set_ylabel("DRAM Bandwidth (GB/s)")
        ax.set_title("System DRAM Bandwidth Over Time")
        ax.set_ylim(bottom=0)
        plt.tight_layout()
        return fig

    _()
    return


@app.cell
def _(cpu_perf):
    def _():
        if cpu_perf.empty:
            return mo.md("No CPU perf data")

        # Average IPC per CPU across all intervals
        avg = cpu_perf.groupby("cpu").agg(
            ipc=("ipc", "mean"),
            memory_bound_pct=("memory_bound_pct", "mean"),
            pcnt_running=("pcnt_running", "min"),
        ).reset_index()

        if "core_type" in cpu_perf.columns:
            core_types = cpu_perf.groupby("cpu")["core_type"].first()
            avg = avg.merge(core_types, on="cpu")

        fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(14, 5))

        # IPC per CPU
        colors = None
        if "core_type" in avg.columns:
            colors = avg["core_type"].map({"P": "#1a1a2e", "E": "#e63946"}).fillna("gray")
        ax1.bar(avg["cpu"].astype(str), avg["ipc"], color=colors)
        ax1.set_xlabel("CPU")
        ax1.set_ylabel("Avg IPC")
        ax1.set_title("Average IPC per CPU")
        ax1.tick_params(axis="x", rotation=90)

        # Memory bound per CPU
        ax2.bar(avg["cpu"].astype(str), avg["memory_bound_pct"], color=colors)
        ax2.set_xlabel("CPU")
        ax2.set_ylabel("Memory Bound %")
        ax2.set_title("Average Memory Bound % per CPU")
        ax2.tick_params(axis="x", rotation=90)

        plt.tight_layout()
        return fig

    _()
    return


@app.cell
def _(cpu_perf):
    def _():
        if cpu_perf.empty:
            return mo.md("No CPU perf data")
        return mo.ui.table(
            cpu_perf.describe().round(2),
            label="CPU Perf Summary",
        )

    _()
    return


@app.cell
def _(sys_perf):
    def _():
        if sys_perf.empty:
            return mo.md("No system perf data")
        return mo.ui.table(
            sys_perf.describe().round(2),
            label="System Perf Summary",
        )

    _()
    return


if __name__ == "__main__":
    app.run()
