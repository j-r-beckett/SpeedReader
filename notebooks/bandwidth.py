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
app = marimo.App(width="medium")

with app.setup:
    import marimo as mo
    import pandas as pd
    import seaborn as sns
    import matplotlib.pyplot as plt
    from pathlib import Path
    import time
    from notebook_utils import format_duration, prioritized_cores, run_benchmark, start_perf

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
            max_cores = 28
        else:
            raise ValueError(f"unknown model {model_input.value}")

        script = Path(__file__).parent / "bandwidth.script.cs"
        warmup = 2
        core_configs = prioritized_cores(max_cores)

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
            spinner.update(subtitle=f"0s / {format_duration(estimated_total)}")
            perf = start_perf()
            for cores in core_configs:
                for start_time, end_time in run_benchmark(make_cmd(cores), duration, warmup):
                    elapsed = time.time() - start_time_estimate
                    spinner.update(subtitle=f"{format_duration(elapsed)} / {format_duration(estimated_total)}")
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
def _(df, perf_df):
    def _():
        # Build TMA metrics dataframe
        rows = []
        for parallelism, g in df.groupby("parallelism"):
            t_start = g["start_time"].min()
            t_end = g["end_time"].max()

            mask = (perf_df["timestamp"] >= t_start) & (perf_df["timestamp"] <= t_end)
            run_perf = perf_df[mask].copy()

            if run_perf.empty:
                continue

            run_perf = run_perf.set_index("timestamp")
            t0 = run_perf.index.min()

            for col in ["memory_bound_pct", "l1_bound_pct", "l2_bound_pct", "l3_bound_pct", "dram_bound_pct", "ipc"]:
                binned = run_perf[col].resample("1s").mean()
                for ts, val in binned.iloc[1:-1].items():
                    rows.append({
                        "parallelism": parallelism,
                        "time_s": (ts - t0).total_seconds(),
                        "metric": col,
                        "value": val,
                    })

        tma_df = pd.DataFrame(rows)

        if tma_df.empty:
            return tma_df

        # Filter to common time range
        max_common_time = tma_df.groupby("parallelism")["time_s"].max().min()
        return tma_df[tma_df["time_s"] <= max_common_time]

    tma_df = _()
    return (tma_df,)


@app.cell
def _(tma_df):
    def _():
        if tma_df.empty:
            return None

        # Memory bound breakdown by parallelism
        fig, ax = plt.subplots(figsize=(10, 4))

        bound_metrics = ["l1_bound_pct", "l2_bound_pct", "l3_bound_pct", "dram_bound_pct"]
        labels = ["L1 Bound", "L2 Bound", "L3 Bound", "DRAM Bound"]

        for metric, label in zip(bound_metrics, labels):
            subset = tma_df[tma_df["metric"] == metric]
            if subset.empty:
                continue
            avg = subset.groupby("parallelism")["value"].mean().reset_index()
            ax.plot(avg["parallelism"], avg["value"], marker="o", label=label)

        ax.axvline(x=8, color="red", linestyle="--", alpha=0.7)
        ax.text(8.1, ax.get_ylim()[1] * 0.95, "Last non-SMT\nP-core", fontsize=9, color="red", va="top")

        max_parallelism = tma_df["parallelism"].max()
        if max_parallelism >= 20:
            ax.axvline(x=20, color="blue", linestyle="--", alpha=0.7)
            ax.text(20.1, ax.get_ylim()[1] * 0.85, "Start SMT P-Cores", fontsize=9, color="blue", va="top")

        ax.set_xlabel("Parallelism")
        ax.set_ylabel("% of Cycles")
        ax.set_title("Memory Bound Breakdown by Parallelism (P-core)")
        ax.set_ylim(bottom=0)
        ax.set_xticks(tma_df["parallelism"].unique())
        ax.legend()
        plt.tight_layout()
        return fig

    mo.vstack([_(), mo.md("Note that L1, L2, L3 stats are not collected for E-cores.\nIn between the non-SMT and SMT P-cores are E-cores")])
    return


@app.cell
def _(tma_df):
    def _():
        if tma_df.empty:
            return None, None

        ipc_df = tma_df[tma_df["metric"] == "ipc"]
        if ipc_df.empty:
            return None, None

        # IPC over time by parallelism
        fig1, ax1 = plt.subplots(figsize=(12, 4))
        sns.lineplot(
            data=ipc_df,
            x="time_s",
            y="value",
            hue="parallelism",
            ax=ax1,
            palette="tab10",
        )
        ax1.set_xlabel("Time (s)")
        ax1.set_ylabel("IPC")
        ax1.set_title("Instructions Per Cycle Over Time")
        handles, labels = ax1.get_legend_handles_labels()
        ax1.legend(handles[::-1], labels[::-1], title="Parallelism", loc="upper left", bbox_to_anchor=(1, 1))
        plt.tight_layout()

        # Avg IPC vs parallelism
        fig2, ax2 = plt.subplots(figsize=(10, 4))
        avg_ipc = ipc_df.groupby("parallelism")["value"].mean().reset_index()
        ax2.plot(avg_ipc["parallelism"], avg_ipc["value"], marker="o", linewidth=2)
        ax2.set_xlabel("Parallelism")
        ax2.set_ylabel("Avg IPC")
        ax2.set_title("Average IPC by Parallelism")
        ax2.set_ylim(bottom=0)
        ax2.set_xticks(avg_ipc["parallelism"])
        plt.tight_layout()

        return fig1, fig2

    mo.vstack([f for f in _() if f is not None])
    return


@app.cell
def _(stats_df):
    mo.ui.table(stats_df, page_size=64, show_column_summaries=False)
    return


@app.cell
def _(bandwidth_df, df):
    def _():
        # Compute duration for each inference
        df["duration_ms"] = (df["end_time"] - df["start_time"]).dt.total_seconds() * 1000

        # Aggregate by parallelism
        agg = df.groupby("parallelism").apply(
            lambda g: pd.Series({
                "mean_duration_ms": g["duration_ms"].mean(),
                "std_duration_ms": g["duration_ms"].std(),
            }),
            include_groups=False,
        ).reset_index()

        # Add avg bandwidth per parallelism
        if not bandwidth_df.empty:
            bw_avg = bandwidth_df.groupby("parallelism")["bandwidth_gbps"].mean()
            agg = agg.merge(bw_avg.rename("bandwidth_gbps").reset_index(), on="parallelism", how="left")

        # Coefficient of variation
        agg["cv"] = agg["std_duration_ms"] / agg["mean_duration_ms"]

        # Forward derivative of CV
        agg["cv_derivative"] = agg["cv"].shift(-1) - agg["cv"]

        # Round for display
        agg["mean_duration_ms"] = agg["mean_duration_ms"].round(2)
        agg["std_duration_ms"] = agg["std_duration_ms"].round(2)
        agg["bandwidth_gbps"] = agg["bandwidth_gbps"].round(2)
        agg["cv"] = agg["cv"].round(4)
        agg["cv_derivative"] = agg["cv_derivative"].round(4)

        return agg

    duration_stats_df = _()
    return (duration_stats_df,)


@app.cell
def _(duration_stats_df, stats_df):
    def _():
        figs = []

        # Std vs parallelism with bandwidth on right y-axis
        fig1, ax1 = plt.subplots(figsize=(10, 3))
        color1 = "#1a1a2e"
        ax1.plot(duration_stats_df["parallelism"], duration_stats_df["std_duration_ms"], marker="o", color=color1, label="Std Duration")
        ax1.set_xlabel("Parallelism")
        ax1.set_ylabel("Std Duration (ms)", color=color1)
        ax1.tick_params(axis="y", labelcolor=color1)
        ax1.set_xticks(duration_stats_df["parallelism"])

        ax1_twin = ax1.twinx()
        color2 = "#e63946"
        ax1_twin.plot(duration_stats_df["parallelism"], duration_stats_df["bandwidth_gbps"], marker="s", color=color2, label="Bandwidth")
        ax1_twin.set_ylabel("Bandwidth (GB/s)", color=color2)
        ax1_twin.tick_params(axis="y", labelcolor=color2)

        ax1.set_title("Duration Std Dev and Bandwidth by Parallelism")
        lines1, labels1 = ax1.get_legend_handles_labels()
        lines2, labels2 = ax1_twin.get_legend_handles_labels()
        ax1.legend(lines1 + lines2, labels1 + labels2, loc="upper left")
        plt.tight_layout()
        figs.append(fig1)

        # CV and CV derivative side by side
        fig2, (ax2, ax3) = plt.subplots(1, 2, figsize=(12, 3))

        ax2.plot(duration_stats_df["parallelism"], duration_stats_df["cv"], marker="o")
        ax2.set_xlabel("Parallelism")
        ax2.set_ylabel("CV")
        ax2.set_title("Coefficient of Variation")
        ax2.set_xticks(duration_stats_df["parallelism"])

        ax3.plot(duration_stats_df["parallelism"], duration_stats_df["cv_derivative"], marker="o")
        ax3.set_xlabel("Parallelism")
        ax3.set_ylabel("CV Derivative")
        ax3.set_title("CV Forward Derivative")
        ax3.set_xticks(duration_stats_df["parallelism"])
        ax3.axhline(y=0, color="gray", linestyle="--", alpha=0.5)

        plt.tight_layout()
        figs.append(fig2)

        # CV derivative (left) and bandwidth (right) vs parallelism
        fig4, ax4 = plt.subplots(figsize=(10, 6))
        color1 = "#1a1a2e"
        ax4.plot(duration_stats_df["parallelism"], duration_stats_df["cv_derivative"], marker="o", color=color1, label="CV Derivative")
        ax4.set_xlabel("Parallelism")
        ax4.set_ylabel("CV Derivative", color=color1)
        ax4.tick_params(axis="y", labelcolor=color1)
        ax4.set_xticks(duration_stats_df["parallelism"])
        ax4.axhline(y=0, color="gray", linestyle="--", alpha=0.5)

        ax4_twin = ax4.twinx()
        color2 = "#e63946"
        ax4_twin.plot(duration_stats_df["parallelism"], duration_stats_df["bandwidth_gbps"], marker="s", color=color2, label="Bandwidth")
        ax4_twin.set_ylabel("Bandwidth (GB/s)", color=color2)
        ax4_twin.tick_params(axis="y", labelcolor=color2)

        ax4.set_title("CV Derivative and Memory Bandwidth by Parallelism")

        # Vertical line at max throughput parallelism
        max_throughput_parallelism = stats_df.loc[stats_df["avg_throughput"].idxmax(), "parallelism"]
        ax4.axvline(x=max_throughput_parallelism, color="green", linestyle="--", alpha=0.7, label=f"Max Throughput (p={max_throughput_parallelism})")

        lines1, labels1 = ax4.get_legend_handles_labels()
        lines2, labels2 = ax4_twin.get_legend_handles_labels()
        ax4.legend(lines1 + lines2, labels1 + labels2, loc="upper left")
        plt.tight_layout()
        figs.append(fig4)

        return figs

    mo.vstack(_())
    return


@app.cell
def _(duration_stats_df):
    mo.ui.table(duration_stats_df, page_size=64, show_column_summaries=False)
    return


if __name__ == "__main__":
    app.run()
