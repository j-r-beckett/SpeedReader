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
    from helpers import build_inference_benchmark, run_inference_benchmark

    sns.set_theme()


@app.cell
def _():
    build_result = build_inference_benchmark()
    return (build_result,)


@app.cell
def _(build_result):
    _ = build_result
    data = run_inference_benchmark(
        model="dbnet",
        duration_seconds=5.0,
        batch_size=1,
        warmup=5,
    )
    return (data,)


@app.cell
def results(data):
    durations = [d for _, d in data]
    avg_ms = sum(durations) / len(durations)
    min_ms = min(durations)
    max_ms = max(durations)

    df = pd.DataFrame({
        "sample": range(1, len(data) + 1),
        "duration_ms": durations,
    })

    fig, ax = plt.subplots(figsize=(10, 4))
    sns.lineplot(data=df, x="sample", y="duration_ms", ax=ax, linewidth=0.8, color="#1a1a2e")
    ax.set_xlabel("Sample")
    ax.set_ylabel("Duration (ms)")
    ax.set_title("Inference Duration per Sample")
    ax.set_ylim(bottom=0)

    mo.vstack([
        mo.md(f"""
## DbNet Inference Benchmark

- **Samples:** {len(data)}
- **Average:** {avg_ms:.2f} ms
- **Min:** {min_ms:.2f} ms
- **Max:** {max_ms:.2f} ms
        """),
        fig,
    ])
    return


if __name__ == "__main__":
    app.run()
