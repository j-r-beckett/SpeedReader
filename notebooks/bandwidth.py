#!/usr/bin/env -S uvx marimo edit --sandbox --no-token --no-skew-protection --watch --port 3005
# /// script
# requires-python = ">=3.11"
# dependencies = ["marimo", "pandas", "seaborn", "notebook_utils"]
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
app = marimo.App()

with app.setup:
    import marimo as mo
    import pandas as pd
    import seaborn as sns
    import matplotlib.pyplot as plt
    from pathlib import Path
    import time
    from notebook_utils import prioritized_cores, run_benchmark, start_perf_bandwidth

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
            duration = 8
            max_cores = 12
        elif model_input.value == "svtr":
            duration = 8
            max_cores = 12
        else:
            raise ValueError(f"unknown model {model_input.value}")

        script = Path(__file__).parent / "bandwidth.script.cs"
        warmup = 2
        core_configs = prioritized_cores(2)

        def make_cmd(cores: list[int]) -> list[str]:
            return [
                "dotnet", "run", str(script), "--",
                "-m", model_input.value,
                "-c", *[str(c) for c in cores],
            ]

        estimated_total = len(core_configs) * (warmup + duration + 1)
        start_time_estimate = time.time()

        rows = []
        with mo.status.spinner(title="Running benchmark...", remove_on_exit=True) as spinner:
            spinner.update(subtitle=f"0s / {int(estimated_total)}s")
            perf = start_perf_bandwidth()
            for cores in core_configs:
                for start_time, end_time in run_benchmark(make_cmd(cores), duration, warmup):
                    elapsed = int(time.time() - start_time_estimate)
                    spinner.update(subtitle=f"{elapsed}s / {int(estimated_total)}s")
                    rows.append({
                        "cores": cores,
                        "start_time": start_time,
                        "end_time": end_time,
                    })
            perf_df = perf.stop()

        df = pd.DataFrame(rows)
        df["parallelism"] = df["cores"].apply(len)

        return df, perf_df

    df, perf_df = _()
    return df, perf_df


@app.cell
def _(df):
    def _():
        # Compute midpoint for each inference
        df["midpoint"] = df["start_time"] + (df["end_time"] - df["start_time"]) / 2

        # Resample to 1-second bins per parallelism level
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
        # For each parallelism level, extract bandwidth samples and bucket to 1-second bins
        rows = []
        for parallelism, g in df.groupby("parallelism"):
            t_start = g["start_time"].min()
            t_end = g["end_time"].max()

            # Filter perf samples to this run's time range
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
            }),
            include_groups=False,
        ).reset_index()

        # Add average bandwidth per parallelism level
        if not bandwidth_df.empty:
            bw_avg = bandwidth_df.groupby("parallelism")["bandwidth_gbps"].mean().round(2)
            stats_df = stats_df.merge(
                bw_avg.rename("avg_bandwidth_gbps").reset_index(),
                on="parallelism",
                how="left",
            )

        stats_df = stats_df.sort_values("parallelism")

        # Marginal efficiency: incremental gain per additional core, relative to baseline
        baseline_throughput = stats_df["avg_throughput"].iloc[0]
        stats_df["throughput_marginal_eff"] = (stats_df["avg_throughput"].diff() / baseline_throughput).round(3)

        return stats_df

    stats_df = _()
    return (stats_df,)


@app.cell
def _(bandwidth_df, throughput_df):
    def _():
        # Throughput over time
        fig1, ax1 = plt.subplots(figsize=(12, 4))
        sns.lineplot(
            data=throughput_df,
            x="time_s",
            y="inferences_per_sec",
            hue="parallelism",
            ax=ax1,
            palette="tab10",
        )
        ax1.set_xlabel("Time (s)")
        ax1.set_ylabel("Inferences / second")
        ax1.set_title("Throughput Over Time by Parallelism")
        ax1.set_ylim(bottom=0)
        handles, labels = ax1.get_legend_handles_labels()
        ax1.legend(handles[::-1], labels[::-1], title="Parallelism", loc="upper left", bbox_to_anchor=(1, 1))
        plt.tight_layout()

        # Bandwidth over time
        fig2, ax2 = plt.subplots(figsize=(12, 4))
        if not bandwidth_df.empty:
            sns.lineplot(
                data=bandwidth_df,
                x="time_s",
                y="bandwidth_gbps",
                hue="parallelism",
                ax=ax2,
                palette="tab10",
            )
            ax2.set_xlabel("Time (s)")
            ax2.set_ylabel("DRAM Bandwidth (GB/s)")
            ax2.set_title("Memory Bandwidth Over Time by Parallelism")
            ax2.set_ylim(bottom=0)
            handles, labels = ax2.get_legend_handles_labels()
            ax2.legend(handles[::-1], labels[::-1], title="Parallelism", loc="upper left", bbox_to_anchor=(1, 1))
            plt.tight_layout()

        return fig1, fig2

    mo.vstack(_())
    return


@app.cell
def _(stats_df):
    def _():
        if "avg_bandwidth_gbps" not in stats_df.columns:
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
        ax2.plot(
            stats_df["parallelism"],
            stats_df["avg_bandwidth_gbps"],
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
def _(stats_df):
    mo.ui.table(stats_df, page_size=64, show_column_summaries=False)
    return


if __name__ == "__main__":
    app.run()
