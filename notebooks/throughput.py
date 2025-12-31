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
    project_path = build_inference_benchmark()
    return (project_path,)


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
def _(model_input, project_path):
    def _():
        if model_input.value == "dbnet":
            duration = 8
            max_threads = 8
        elif model_input.value == "svtr":
            duration = 4
            max_threads = 16
        else:
            raise ValueError(f"unknown model {model_input.value}")

        return run_benchmark_sweep(
            project_path=project_path,
            model=model_input.value,
            duration_seconds=duration,
            warmup_seconds=1,
            parallelism=list(range(1, max_threads + 1)),
        )

    df = _()
    return (df,)


@app.cell
def _(df):
    def _():
        # Compute midpoint for each inference
        df["midpoint"] = df["start_time"] + (df["end_time"] - df["start_time"]) / 2

        # Resample to 1-second bins per parallelism
        rows = []
        for parallelism, g in df.groupby("parallelism"):
            counts = g.set_index("midpoint").resample("1s").size()
            t0 = counts.index.min()
            for ts, count in counts.iloc[1:-1].items():  # Drop incomplete first/last bins
                rows.append({
                    "parallelism": parallelism,
                    "time_s": (ts - t0).total_seconds(),
                    "inferences_per_sec": count,
                })

        throughput_df = pd.DataFrame(rows)

        # Filter to common time range (all groups have same extent)
        max_common_time = throughput_df.groupby("parallelism")["time_s"].max().min()
        return throughput_df[throughput_df["time_s"] <= max_common_time]

    throughput_df = _()
    return (throughput_df,)


@app.cell
def _(df):
    def _():
        stats_df = df.groupby("parallelism").apply(
            lambda g: pd.Series({
                "avg_throughput": round(len(g) / (g["end_time"].max() - g["start_time"].min()).total_seconds(), 2),
                "avg_duration_ms": round(((g["end_time"] - g["start_time"]).dt.total_seconds() * 1000).mean(), 2),
                "std_duration_ms": round(((g["end_time"] - g["start_time"]).dt.total_seconds() * 1000).std(), 2),
            }),
            include_groups=False,
        ).reset_index()

        stats_df = stats_df.sort_values("parallelism")
        baseline = stats_df.loc[stats_df["parallelism"] == 1, "avg_throughput"].iloc[0]
        stats_df["marginal_efficiency"] = (stats_df["avg_throughput"].diff() / baseline).round(3)
        return stats_df

    stats_df = _()
    return (stats_df,)


@app.cell
def _(stats_df, throughput_df):
    def _():
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
        sns.lineplot(
            data=stats_df,
            x="parallelism",
            y="avg_throughput",
            ax=axes[1],
            marker="o",
            color="#1a1a2e",
        )
        axes[1].set_xlabel("Parallelism")
        axes[1].set_ylabel("Avg Inferences / second")
        axes[1].set_title("Average Throughput by Parallelism")
        axes[1].set_ylim(bottom=0)

        plt.tight_layout()

        return fig

    _()
    return


@app.cell
def _(stats_df):
    mo.ui.table(stats_df, page_size=64, show_column_summaries=False)
    return


if __name__ == "__main__":
    app.run()
