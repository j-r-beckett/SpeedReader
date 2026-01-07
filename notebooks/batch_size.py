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
    return (model_input,)


@app.cell
def _(model_input):
    def _():
        if model_input.value == "dbnet":
            duration = 6
            batch_sizes = [1, 2, 4, 8]
        elif model_input.value == "svtr":
            duration = 6
            batch_sizes = [1, 2, 4, 8, 16, 32]
        else:
            raise ValueError(f"unknown model {model_input.value}")

        script = Path(__file__).parent / "batch_size.script.cs"
        warmup = 2

        def make_cmd(batch_size: int) -> list[str]:
            return [
                "dotnet", "run", str(script), "--",
                "-m", model_input.value,
                "-b", str(batch_size),
            ]

        estimated_total = len(batch_sizes) * (warmup + duration + 1)
        start_time_estimate = time.time()

        rows = []
        with mo.status.spinner(title="Running benchmark...", remove_on_exit=True) as spinner:
            spinner.update(subtitle=f"0s / {format_duration(estimated_total)}")
            for batch_size in batch_sizes:
                for _, start_time, end_time in run_benchmark(make_cmd(batch_size), duration, warmup):
                    elapsed = time.time() - start_time_estimate
                    spinner.update(subtitle=f"{format_duration(elapsed)} / {format_duration(estimated_total)}")
                    rows.append({
                        "batch_size": batch_size,
                        "start_time": start_time,
                        "end_time": end_time,
                    })

        return pd.DataFrame(rows)

    df = _()
    return (df,)


@app.cell
def _(df):
    def _():
        rows = []
        for batch_size, g in df.groupby("batch_size"):
            total_duration_s = (g["end_time"].max() - g["start_time"].min()).total_seconds()
            num_inferences = len(g)
            total_images = num_inferences * batch_size

            rows.append({
                "batch_size": batch_size,
                "num_inferences": num_inferences,
                "total_images": total_images,
                "images_per_sec": round(total_images / total_duration_s, 2),
            })

        return pd.DataFrame(rows)

    stats_df = _()
    return (stats_df,)


@app.cell
def _(model_input, stats_df):
    def _():
        fig, ax = plt.subplots(figsize=(8, 5))

        ax.bar(stats_df["batch_size"].astype(str), stats_df["images_per_sec"], color="#1a1a2e", alpha=0.8)
        ax.set_xlabel("Batch Size")
        ax.set_ylabel("Images / second")
        ax.set_title(f"Throughput by Batch Size ({model_input.value})")
        ax.set_ylim(bottom=0)

        baseline = stats_df[stats_df["batch_size"] == 1]["images_per_sec"].iloc[0]
        for i, row in stats_df.iterrows():
            pct = (row["images_per_sec"] / baseline - 1) * 100
            sign = "+" if pct >= 0 else ""
            ax.annotate(
                f"{sign}{pct:.0f}%",
                (str(row["batch_size"]), row["images_per_sec"]),
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
def _(stats_df):
    mo.ui.table(stats_df, page_size=16, show_column_summaries=False)
    return


if __name__ == "__main__":
    app.run()
