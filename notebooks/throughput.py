# /// script
# requires-python = ">=3.11"
# dependencies = ["marimo[mcp]", "pandas", "seaborn"]
# ///

import marimo

__generated_with = "0.18.4"
app = marimo.App()

with app.setup:
    import marimo as mo
    import numpy as np
    import pandas as pd
    import seaborn as sns
    import matplotlib.pyplot as plt
    from helpers import build_inference_benchmark, run_benchmark_sweep

    sns.set_theme()


@app.cell
def _():
    project_path = build_inference_benchmark()
    return (project_path,)


@app.cell
def _(project_path):
    df = run_benchmark_sweep(
        project_path=project_path,
        model="dbnet",
        duration_seconds=8.0,
        warmup=2.0,
        parallelism=[1, 2, 3, 4, 5, 6, 7, 8],
    )
    return (df,)


@app.cell
def _(df):
    def _():
        slot_ms = 1000
        slot_s = slot_ms / 1000.0
        result_rows = []

        for parallelism, group in df.groupby("parallelism"):
            group = group.sort_values("timestamp")
            t0 = group["timestamp"].min()
            group = group.copy()

            # Compute event intervals in seconds relative to t0
            group["end_s"] = (group["timestamp"] - t0).dt.total_seconds()
            group["start_s"] = group["end_s"] - group["duration_ms"] / 1000.0
            group["duration_s"] = group["duration_ms"] / 1000.0
            group["speed"] = 1000.0 / group["duration_ms"]  # inferences per second

            # Create slots (floor to exclude partial final slot)
            max_time = group["end_s"].max()
            num_slots = int(max_time / slot_s)

            for slot_idx in range(num_slots):
                slot_start = slot_idx * slot_s
                slot_end = slot_start + slot_s

                # Find events where >= 50% of duration overlaps this slot
                def overlap_fraction(row):
                    overlap_start = max(row["start_s"], slot_start)
                    overlap_end = min(row["end_s"], slot_end)
                    overlap = max(0, overlap_end - overlap_start)
                    return overlap / row["duration_s"] if row["duration_s"] > 0 else 0

                overlaps = group.apply(overlap_fraction, axis=1)
                assigned = group[overlaps >= 0.5]

                # Count observations in slot, divide by slot duration for inferences/sec
                throughput = len(assigned) / slot_s
                result_rows.append({
                    "time_s": slot_start,
                    "inferences_per_sec": throughput,
                    "parallelism": parallelism,
                })
        return pd.DataFrame(result_rows)

    throughput_df = _()
    return (throughput_df,)


@app.cell
def _(df):
    def _():
        stats_rows = []
        for parallelism, group in df.groupby("parallelism"):
            t0 = group["timestamp"].min()
            t1 = group["timestamp"].max()
            total_time_s = (t1 - t0).total_seconds()
            total_inferences = len(group)
            avg_throughput_val = total_inferences / total_time_s if total_time_s > 0 else 0
            avg_duration = group["duration_ms"].mean()
            std_duration = group["duration_ms"].std()
            stats_rows.append({
                "parallelism": parallelism,
                "avg_throughput": round(avg_throughput_val, 2),
                "avg_duration_ms": round(avg_duration, 2),
                "std_duration_ms": round(std_duration, 2),
            })
        result = pd.DataFrame(stats_rows).sort_values("parallelism")
        baseline = result.loc[result["parallelism"] == 1, "avg_throughput"].iloc[0]
        result["marginal_efficiency"] = (result["avg_throughput"].diff() / baseline).round(3)
        return result

    stats_df = _()
    return (stats_df,)


@app.cell
def _(throughput_df):
    fig, axes = plt.subplots(2, 1, figsize=(12, 8))

    # Throughput over time
    sns.lineplot(
        data=throughput_df,
        x="time_s",
        y="inferences_per_sec",
        hue="parallelism",
        ax=axes[0],
        palette="tab10",
    )
    axes[0].set_xlabel("Time (s)")
    axes[0].set_ylabel("Inferences / second")
    axes[0].set_title("Throughput Over Time by Parallelism")
    axes[0].set_ylim(bottom=0)
    handles, labels = axes[0].get_legend_handles_labels()
    axes[0].legend(handles[::-1], labels[::-1], title="Parallelism", loc="upper left", bbox_to_anchor=(1, 1))

    # Summary: average throughput per parallelism
    avg_throughput = throughput_df.groupby("parallelism")["inferences_per_sec"].mean().reset_index()
    sns.lineplot(
        data=avg_throughput,
        x="parallelism",
        y="inferences_per_sec",
        ax=axes[1],
        marker="o",
        color="#1a1a2e",
    )
    axes[1].set_xlabel("Parallelism")
    axes[1].set_ylabel("Avg Inferences / second")
    axes[1].set_title("Average Throughput by Parallelism")
    axes[1].set_ylim(bottom=0)

    plt.tight_layout()
    fig
    return


@app.cell
def _(stats_df):
    mo.ui.table(stats_df)
    return


if __name__ == "__main__":
    app.run()
