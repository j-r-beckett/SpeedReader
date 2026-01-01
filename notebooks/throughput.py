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
    from helpers import build_inference_benchmark, run_benchmark_sweep, start_perf_bandwidth, get_hardware_summary, get_physical_p_cores

    sns.set_theme()


@app.cell
def _():
    project_path = build_inference_benchmark()
    return (project_path,)


@app.cell
def _():
    p_cores = get_physical_p_cores()
    p_cores_info = f"**Physical P-cores:** {p_cores}" if p_cores else ""
    mo.md(f"""
    ## Hardware

    ```
    {get_hardware_summary()}
    ```

    {p_cores_info}
    """)
    return


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
            max_threads = 12
        elif model_input.value == "svtr":
            duration = 8
            max_threads = 12
        else:
            raise ValueError(f"unknown model {model_input.value}")

        perf = start_perf_bandwidth()
        df = run_benchmark_sweep(
            project_path=project_path,
            model=model_input.value,
            duration_seconds=duration,
            warmup_seconds=2,
            parallelism=list(range(1, max_threads + 1)),
            # parallelism=[0]
        )
        perf_df = perf.stop()
        return df, perf_df

    df, perf_df = _()
    return df, perf_df


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
def _(df, perf_df):
    def _():
        # For each parallelism run, extract bandwidth samples and bucket to 1-second bins
        rows = []
        for parallelism, g in df.groupby("parallelism"):
            t_start = g["start_time"].min()
            t_end = g["end_time"].max()

            # Filter perf samples to this parallelism run's time range
            mask = (perf_df["timestamp"] >= t_start) & (perf_df["timestamp"] <= t_end)
            run_perf = perf_df[mask].copy()

            if run_perf.empty:
                continue

            # Resample to 1-second bins, averaging bandwidth
            run_perf = run_perf.set_index("timestamp")
            binned = run_perf["bandwidth_gbps"].resample("1s").mean()
            t0 = binned.index.min()

            for ts, bw in binned.iloc[1:-1].items():  # Drop incomplete first/last bins
                rows.append({
                    "parallelism": parallelism,
                    "time_s": (ts - t0).total_seconds(),
                    "bandwidth_gbps": bw,
                })

        bandwidth_df = pd.DataFrame(rows)

        # Filter to common time range (match throughput_df)
        if not bandwidth_df.empty:
            max_common_time = bandwidth_df.groupby("parallelism")["time_s"].max().min()
            bandwidth_df = bandwidth_df[bandwidth_df["time_s"] <= max_common_time]

        return bandwidth_df

    bandwidth_df = _()
    return (bandwidth_df,)


@app.cell
def _(bandwidth_df, df):
    def _():
        stats_df = df.groupby("parallelism").apply(
            lambda g: pd.Series({
                "avg_throughput": round(len(g) / (g["end_time"].max() - g["start_time"].min()).total_seconds(), 2),
                "avg_duration_ms": round(((g["end_time"] - g["start_time"]).dt.total_seconds() * 1000).mean(), 2),
                "std_duration_ms": round(((g["end_time"] - g["start_time"]).dt.total_seconds() * 1000).std(), 2),
            }),
            include_groups=False,
        ).reset_index()

        # Add average bandwidth per parallelism
        if not bandwidth_df.empty:
            bw_avg = bandwidth_df.groupby("parallelism")["bandwidth_gbps"].mean().round(2)
            stats_df = stats_df.merge(
                bw_avg.rename("avg_bandwidth_gbps").reset_index(),
                on="parallelism",
                how="left",
            )

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
    def _():
        fig, ax = plt.subplots(figsize=(10, 5))
        ax.plot(
            stats_df["parallelism"],
            stats_df["marginal_efficiency"],
            marker="o",
            color="#1a1a2e",
            linewidth=2,
        )
        ax.axvline(x=8, color="#e63946", linestyle="--", linewidth=2, label="P-core boundary")
        ax.set_xlabel("Parallelism")
        ax.set_ylabel("Marginal Efficiency")
        ax.set_title("Marginal Efficiency by Parallelism")
        ax.set_ylim(top=1.1)
        ax.legend()

        plt.tight_layout()
        return fig

    _()
    return


@app.cell
def _(bandwidth_df):
    def _():
        if bandwidth_df.empty:
            return None

        fig, axes = plt.subplots(2, 1, figsize=(12, 8))

        # Bandwidth over time
        sns.lineplot(
            data=bandwidth_df,
            x="time_s",
            y="bandwidth_gbps",
            hue="parallelism",
            ax=axes[0],
            palette="tab10",
        )
        axes[0].set_xlabel("Time (s)")
        axes[0].set_ylabel("DRAM Bandwidth (GB/s)")
        axes[0].set_title("Memory Bandwidth Over Time by Parallelism")
        axes[0].set_ylim(bottom=0)
        handles, labels = axes[0].get_legend_handles_labels()
        axes[0].legend(handles[::-1], labels[::-1], title="Parallelism", loc="upper left", bbox_to_anchor=(1, 1))

        # Average bandwidth per parallelism
        bw_stats = bandwidth_df.groupby("parallelism")["bandwidth_gbps"].mean().reset_index()
        sns.lineplot(
            data=bw_stats,
            x="parallelism",
            y="bandwidth_gbps",
            ax=axes[1],
            marker="o",
            color="#1a1a2e",
        )
        axes[1].set_xlabel("Parallelism")
        axes[1].set_ylabel("Avg DRAM Bandwidth (GB/s)")
        axes[1].set_title("Average Memory Bandwidth by Parallelism")
        axes[1].set_ylim(bottom=0)

        plt.tight_layout()
        return fig

    _()
    return


@app.cell
def _(bandwidth_df, stats_df):
    def _():
        if bandwidth_df.empty:
            return None

        fig, ax1 = plt.subplots(figsize=(10, 6))

        # Left y-axis: throughput
        color1 = "#1a1a2e"
        ax1.plot(
            stats_df["parallelism"],
            stats_df["avg_throughput"],
            marker="o",
            color=color1,
            label="Throughput",
        )
        ax1.set_xlabel("Parallelism")
        ax1.set_ylabel("Avg Inferences / second", color=color1)
        ax1.tick_params(axis="y", labelcolor=color1)
        ax1.set_ylim(bottom=0)

        # Right y-axis: bandwidth
        ax2 = ax1.twinx()
        color2 = "#e63946"
        bw_stats = bandwidth_df.groupby("parallelism")["bandwidth_gbps"].mean().reset_index()
        ax2.plot(
            bw_stats["parallelism"],
            bw_stats["bandwidth_gbps"],
            marker="s",
            color=color2,
            label="Bandwidth",
        )
        ax2.set_ylabel("Avg DRAM Bandwidth (GB/s)", color=color2)
        ax2.tick_params(axis="y", labelcolor=color2)
        ax2.set_ylim(bottom=0)

        ax1.set_title("Throughput and Memory Bandwidth by Parallelism")

        # Combined legend
        lines1, labels1 = ax1.get_legend_handles_labels()
        lines2, labels2 = ax2.get_legend_handles_labels()
        ax1.legend(lines1 + lines2, labels1 + labels2, loc="lower right")

        plt.tight_layout()
        return fig

    _()
    return


@app.cell
def _(bandwidth_df, stats_df):
    def _():
        if bandwidth_df.empty:
            return None

        # Merge throughput and bandwidth by parallelism
        bw_stats = bandwidth_df.groupby("parallelism")["bandwidth_gbps"].mean().reset_index()
        merged = stats_df.merge(bw_stats, on="parallelism")

        # Compute R² (square of Pearson correlation)
        r = merged["avg_throughput"].corr(merged["bandwidth_gbps"])
        r_squared = r ** 2

        # Compute linear regression coefficients: throughput = slope * bandwidth + intercept
        x = merged["bandwidth_gbps"]
        y = merged["avg_throughput"]
        slope = (x * y).mean() - x.mean() * y.mean()
        slope /= (x ** 2).mean() - x.mean() ** 2
        intercept = y.mean() - slope * x.mean()

        # GB per inference (inverse of slope)
        gb_per_inference = 1 / slope

        return mo.md(f"""
    ## Linear Regression: Throughput vs Bandwidth

    | Metric | Value |
    |--------|-------|
    | **R²** | {r_squared:.6f} |
    | **Slope** | {slope:.4f} inf/s per GB/s |
    | **Intercept** | {intercept:.4f} inf/s |
    | **GB per inference** | {gb_per_inference:.3f} GB |

    Model: `throughput = {slope:.4f} × bandwidth + {intercept:.4f}`
    """)

    _()
    return


@app.cell
def _(bandwidth_df, stats_df):
    def _():
        if bandwidth_df.empty:
            return None

        # Get bandwidth stats
        bw_stats = bandwidth_df.groupby("parallelism")["bandwidth_gbps"].mean().reset_index()
        merged = stats_df.merge(bw_stats, on="parallelism")

        # Compute linear fit: bandwidth = slope * throughput + intercept
        x = merged["avg_throughput"]
        y = merged["bandwidth_gbps"]
        slope = (x * y).mean() - x.mean() * y.mean()
        slope /= (x ** 2).mean() - x.mean() ** 2
        intercept = y.mean() - slope * x.mean()

        # Generate fit line
        x_fit = [x.min(), x.max()]
        y_fit = [slope * xi + intercept for xi in x_fit]

        fig, ax = plt.subplots(figsize=(10, 6))

        # Actual data
        ax.plot(x, y, marker="o", color="#1a1a2e", label="Actual", linewidth=2)

        # Linear fit
        ax.plot(x_fit, y_fit, linestyle="--", color="#457b9d", label="Linear fit", linewidth=2)

        ax.set_xlabel("Avg Inferences / second")
        ax.set_ylabel("Avg DRAM Bandwidth (GB/s)")
        ax.set_title("Bandwidth vs Throughput (with Linear Fit)")
        ax.set_xlim(left=0)
        ax.set_ylim(bottom=0)
        ax.legend()

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
