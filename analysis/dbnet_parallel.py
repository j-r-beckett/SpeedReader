#!/usr/bin/env -S uv run marimo edit --no-token --no-skew-protection --watch --port 3006

import marimo

__generated_with = "0.23.3"
app = marimo.App(width="medium")

with app.setup:
    import marimo as mo
    import pandas as pd
    import seaborn as sns
    import matplotlib.pyplot as plt
    from bench import build, run_dbnet

    sns.set_theme()


@app.cell
def _():
    build()
    return


@app.cell
def _():
    df = run_dbnet(
        configs=[[j * 2 for j in range(i)] for i in range(1, 13)],
        duration=8,
        trim=1.0,
    )
    return (df,)


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

        color1 = "#264653"
        ax1.plot(stats["parallelism"], stats["avg_throughput"], marker="o", color=color1, label="Throughput")
        ax1.set_xlabel("Parallelism")
        ax1.set_ylabel("Avg Inferences / second", color=color1)
        ax1.tick_params(axis="y", labelcolor=color1)
        ax1.set_ylim(bottom=0)

        ax2 = ax1.twinx()
        color2 = "#e76f51"
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
def _(df):
    def _():
        bound_cols = ["l1_bound_pct", "l2_bound_pct", "l3_bound_pct", "dram_bound_pct"]
        stats = df.groupby("parallelism").apply(
            lambda g: pd.Series({
                "throughput": len(g) / (g["end_mono"].max() - g["start_mono"].min()),
                **{c: g[c].mean() for c in bound_cols},
            }),
            include_groups=False,
        ).reset_index()

        fig, ax1 = plt.subplots(figsize=(10, 6))

        throughput_color = "#264653"
        ax1.plot(stats["parallelism"], stats["throughput"], marker="o", color=throughput_color, label="Throughput", linewidth=2)
        ax1.set_xlabel("Parallelism")
        ax1.set_ylabel("Avg Inferences / second", color=throughput_color)
        ax1.tick_params(axis="y", labelcolor=throughput_color)
        ax1.set_ylim(bottom=0)

        ax2 = ax1.twinx()
        bound_colors = ["#2a9d8f", "#e9c46a", "#f4a261", "#e76f51"]
        bound_labels = ["L1", "L2", "L3", "DRAM"]
        for col, color, label in zip(bound_cols, bound_colors, bound_labels):
            ax2.plot(stats["parallelism"], stats[col], marker="s", color=color, label=label)
        ax2.set_ylabel("Memory Bound %")
        ax2.set_ylim(bottom=0)

        ax1.set_title("Throughput vs Memory Hierarchy Bound by Parallelism")

        lines1, labels1 = ax1.get_legend_handles_labels()
        lines2, labels2 = ax2.get_legend_handles_labels()
        ax1.legend(lines1 + lines2, labels1 + labels2, loc="upper left")

        plt.tight_layout()
        return fig

    _()
    return


@app.cell
def _(df):
    def _():
        stats = df.groupby("parallelism").apply(
            lambda g: pd.Series({
                "throughput": len(g) / (g["end_mono"].max() - g["start_mono"].min()),
                "bandwidth_gbps": g["bandwidth_gbps"].iloc[0],
            }),
            include_groups=False,
        ).reset_index()

        fig, ax1 = plt.subplots(figsize=(10, 6))

        throughput_color = "#264653"
        ax1.plot(stats["parallelism"], stats["throughput"], marker="o", color=throughput_color, label="Throughput", linewidth=2)
        ax1.set_xlabel("Parallelism")
        ax1.set_ylabel("Avg Inferences / second", color=throughput_color)
        ax1.tick_params(axis="y", labelcolor=throughput_color)
        ax1.set_ylim(bottom=0)

        ax2 = ax1.twinx()
        bandwidth_color = "#e76f51"
        ax2.plot(stats["parallelism"], stats["bandwidth_gbps"], marker="s", color=bandwidth_color, label="DRAM Bandwidth", linewidth=2)
        ax2.set_ylabel("DRAM Bandwidth (GB/s)", color=bandwidth_color)
        ax2.tick_params(axis="y", labelcolor=bandwidth_color)
        ax2.set_ylim(bottom=0)

        ax1.set_title("Throughput vs DRAM Bandwidth by Parallelism")

        lines1, labels1 = ax1.get_legend_handles_labels()
        lines2, labels2 = ax2.get_legend_handles_labels()
        ax1.legend(lines1 + lines2, labels1 + labels2, loc="lower right")

        plt.tight_layout()
        return fig

    _()
    return


if __name__ == "__main__":
    app.run()
