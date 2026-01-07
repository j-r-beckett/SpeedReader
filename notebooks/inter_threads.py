#!/usr/bin/env -S uvx marimo edit --sandbox --no-token --no-skew-protection --watch --port 3007
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


@app.cell
def _():
    default_model = mo.cli_args().get("model") or "dbnet"
    model_input = mo.ui.dropdown(
        options=["dbnet", "svtr"],
        value=default_model,
        label="Model",
    )
    model_input
    return


@app.cell
def _():
    def _():
        duration = 6
        thread_counts = [1, 2, 4, 8]

        script = Path(__file__).parent / "inter_threads.script.cs"
        warmup = 2

        def make_cmd(threads: int) -> list[str]:
            return [
                "dotnet", "run", str(script), "--",
                "-m", model_input.value,
                "-t", str(threads),
            ]

        estimated_total = len(thread_counts) * (warmup + duration + 1)
        start_time_estimate = time.time()

        rows = []
        with mo.status.spinner(title="Running benchmark...", remove_on_exit=True) as spinner:
            spinner.update(subtitle=f"0s / {format_duration(estimated_total)}")
            for threads in thread_counts:
                for _, start_time, end_time in run_benchmark(make_cmd(threads), duration, warmup):
                    elapsed = time.time() - start_time_estimate
                    spinner.update(subtitle=f"{format_duration(elapsed)} / {format_duration(estimated_total)}")
                    rows.append({
                        "inter_threads": threads,
                        "start_time": start_time,
                        "end_time": end_time,
                    })

        return pd.DataFrame(rows)

    df = _()
    return


@app.cell
def _():
    def _():
        rows = []
        for threads, g in df.groupby("inter_threads"):
            total_duration_s = (g["end_time"].max() - g["start_time"].min()).total_seconds()
            num_inferences = len(g)

            rows.append({
                "inter_threads": threads,
                "num_inferences": num_inferences,
                "inferences_per_sec": round(num_inferences / total_duration_s, 2),
            })

        return pd.DataFrame(rows)

    stats_df = _()
    return


@app.cell
def _():
    def _():
        fig, ax = plt.subplots(figsize=(8, 5))

        ax.bar(stats_df["inter_threads"].astype(str), stats_df["inferences_per_sec"], color="#1a1a2e", alpha=0.8)
        ax.set_xlabel("Inter-op Threads")
        ax.set_ylabel("Inferences / second")
        ax.set_title(f"Throughput by Inter-op Thread Count ({model_input.value})")
        ax.set_ylim(bottom=0)

        baseline = stats_df[stats_df["inter_threads"] == 1]["inferences_per_sec"].iloc[0]
        for i, row in stats_df.iterrows():
            pct = (row["inferences_per_sec"] / baseline - 1) * 100
            sign = "+" if pct >= 0 else ""
            ax.annotate(
                f"{sign}{pct:.0f}%",
                (str(row["inter_threads"]), row["inferences_per_sec"]),
                textcoords="offset points",
                xytext=(0, 5),
                ha="center",
                fontsize=9,
            )

        plt.tight_layout()
        return fig

    _()
    return


@app.cell
def _():
    mo.ui.table(stats_df, page_size=16, show_column_summaries=False)
    return


if __name__ == "__main__":
    app.run()
