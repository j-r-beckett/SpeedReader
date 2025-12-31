# /// script
# requires-python = ">=3.11"
# dependencies = ["marimo[mcp]", "pandas", "seaborn"]
# ///

import marimo

__generated_with = "0.18.4"
app = marimo.App()

with app.setup:
    import marimo as mo
    import pandas as pd
    import seaborn as sns
    import matplotlib.pyplot as plt
    from datetime import datetime
    from helpers import build_inference_benchmark, run_benchmark_sweep

    sns.set_theme()


@app.cell
def _():
    build_result = build_inference_benchmark()
    return (build_result,)


@app.cell
def _(build_result):
    _ = build_result
    sweep_results = run_benchmark_sweep(
        model="dbnet",
        parallelism=[1, 2, 4],
        iterations=120,
        warmup=5,
        timestamped=True,
    )
    return (sweep_results,)


@app.cell
def _(sweep_results):
    def _():
        rows = []
        for cfg, measurements in sweep_results:
            parallelism = cfg["parallelism"]
            for ts_str, duration_ms in measurements:
                ts = datetime.fromisoformat(ts_str.replace("Z", "+00:00"))
                rows.append({
                    "parallelism": parallelism,
                    "timestamp": ts,
                    "duration_ms": duration_ms,
                })
        return pd.DataFrame(rows)

    df = _()
    return (df,)


@app.cell
def _(df):
    def _():
        result_rows = []
        for parallelism, group in df.groupby("parallelism"):
            group = group.sort_values("timestamp")
            t0 = group["timestamp"].min()
            group = group.copy()
            group["elapsed_s"] = (group["timestamp"] - t0).dt.total_seconds()

            # Bucket into 1-second windows
            group["window"] = group["elapsed_s"].astype(int)
            throughput = group.groupby("window").size().reset_index(name="inferences_per_sec")
            throughput["parallelism"] = parallelism
            result_rows.append(throughput)
        return pd.concat(result_rows, ignore_index=True)

    throughput_df = _()
    return (throughput_df,)


@app.cell
def _(df, throughput_df):
    fig, axes = plt.subplots(1, 2, figsize=(14, 5))

    # Throughput over time
    sns.lineplot(
        data=throughput_df,
        x="window",
        y="inferences_per_sec",
        hue="parallelism",
        marker="o",
        ax=axes[0],
        palette="viridis",
    )
    axes[0].set_xlabel("Time (seconds)")
    axes[0].set_ylabel("Inferences / second")
    axes[0].set_title("Throughput Over Time by Parallelism")
    axes[0].legend(title="Parallelism")

    # Summary: average throughput per parallelism
    avg_throughput = throughput_df.groupby("parallelism")["inferences_per_sec"].mean().reset_index()
    sns.barplot(
        data=avg_throughput,
        x="parallelism",
        y="inferences_per_sec",
        ax=axes[1],
        color="#1a1a2e",
    )
    axes[1].set_xlabel("Parallelism")
    axes[1].set_ylabel("Avg Inferences / second")
    axes[1].set_title("Average Throughput by Parallelism")

    plt.tight_layout()

    # Compute stats
    stats_rows = []
    for parallelism, group in df.groupby("parallelism"):
        t0 = group["timestamp"].min()
        t1 = group["timestamp"].max()
        total_time_s = (t1 - t0).total_seconds()
        total_inferences = len(group)
        avg_throughput_val = total_inferences / total_time_s if total_time_s > 0 else 0
        avg_duration = group["duration_ms"].mean()
        stats_rows.append({
            "parallelism": parallelism,
            "total_inferences": total_inferences,
            "total_time_s": round(total_time_s, 2),
            "avg_throughput": round(avg_throughput_val, 2),
            "avg_duration_ms": round(avg_duration, 2),
        })
    stats_df = pd.DataFrame(stats_rows)

    mo.vstack([
        mo.md("""
## Throughput Analysis

This notebook analyzes inference throughput across different parallelism levels.

**Key questions:**
- Does parallelism improve throughput?
- Is throughput stable over time, or does it degrade (thermal throttling, memory pressure)?
- What's the efficiency gain per additional thread?
        """),
        fig,
        mo.md("### Summary Statistics"),
        mo.ui.table(stats_df),
    ])
    return


if __name__ == "__main__":
    app.run()
