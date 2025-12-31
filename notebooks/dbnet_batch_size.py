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
        batch_size=[1, 2, 4, 8],
        iterations=40,
        warmup=5,
    )
    return (sweep_results,)


@app.cell
def _(sweep_results):
    def _():
        rows = []
        for cfg, measurements in sweep_results:
            batch_size = cfg["batch_size"]
            avg_ms = sum(measurements) / len(measurements)
            per_image_ms = avg_ms / batch_size
            rows.append({
                "batch_size": batch_size,
                "avg_total_ms": avg_ms,
                "per_image_ms": per_image_ms,
                "measurements": measurements,
            })
        return pd.DataFrame(rows)

    df = _()
    return (df,)


@app.cell
def _(df):
    fig, axes = plt.subplots(1, 2, figsize=(12, 4))

    sns.barplot(data=df, x="batch_size", y="avg_total_ms", ax=axes[0], color="#1a1a2e")
    axes[0].set_xlabel("Batch Size")
    axes[0].set_ylabel("Total Duration (ms)")
    axes[0].set_title("Total Inference Duration by Batch Size")

    sns.barplot(data=df, x="batch_size", y="per_image_ms", ax=axes[1], color="#4a4a6a")
    axes[1].set_xlabel("Batch Size")
    axes[1].set_ylabel("Per-Image Duration (ms)")
    axes[1].set_title("Per-Image Duration by Batch Size")

    plt.tight_layout()

    mo.vstack([
        mo.md("""
## DbNet Batch Size Analysis

Does batch size affect per-image inference duration?

If per-image duration is **constant** across batch sizes, batching provides no throughput benefit.
If per-image duration **decreases** with larger batches, batching amortizes overhead.
        """),
        fig,
        mo.md("### Raw Data"),
        mo.ui.table(df[["batch_size", "avg_total_ms", "per_image_ms"]]),
    ])
    return


if __name__ == "__main__":
    app.run()
