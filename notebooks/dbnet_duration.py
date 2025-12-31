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

    sns.set_theme()  # apply seaborn theme to all matplotlib charts


@app.cell
def _():
    build_result = build_inference_benchmark()
    return (build_result,)


@app.cell
def _(build_result):
    _ = build_result  # make this cell depend on the build cell
    data = run_inference_benchmark(
        model="dbnet",
        batch_size=1,
        iterations=40,
        warmup=0,
    )
    return (data,)


@app.cell
def results(data):
    avg_ms = sum(data) / len(data)
    min_ms = min(data)
    max_ms = max(data)

    df = pd.DataFrame({
        "iteration": range(1, len(data) + 1),
        "duration_ms": data,
    })

    fig, ax = plt.subplots(figsize=(10, 4))
    sns.lineplot(data=df, x="iteration", y="duration_ms", ax=ax, linewidth=0.8, color="#1a1a2e")
    ax.set_xlabel("Iteration")
    ax.set_ylabel("Duration (ms)")
    ax.set_title("Inference Duration per Iteration")
    ax.set_ylim(bottom=0)

    mo.vstack([
        mo.md(f"""
    ## DbNet Inference Benchmark

    - **Iterations:** {len(data)}
    - **Average:** {avg_ms:.2f} ms
    - **Min:** {min_ms:.2f} ms
    - **Max:** {max_ms:.2f} ms
    """),
        fig,
    ])
    return


if __name__ == "__main__":
    app.run()
