#!/usr/bin/env -S uvx marimo edit --sandbox --no-token --no-skew-protection --watch --port 3005
# /// script
# requires-python = ">=3.11"
# dependencies = ["marimo", "numpy", "pandas", "seaborn", "scikit-learn", "notebook_utils"]
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
                for core_id, start_time, end_time in run_benchmark(make_cmd(cores), duration, warmup):
                    elapsed = time.time() - start_time_estimate
                    spinner.update(subtitle=f"{format_duration(elapsed)} / {format_duration(estimated_total)}")
                    rows.append({
                        "cores": cores,
                        "core_id": core_id,
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
def _(df):
    def _():
        # Compute duration for each inference
        df["duration_ms"] = (df["end_time"] - df["start_time"]).dt.total_seconds() * 1000

        rows = []
        for parallelism, g in df.groupby("parallelism"):
            # Total std dev across all cores
            total_std = g["duration_ms"].std()
            total_var = g["duration_ms"].var()

            # Intra-core std dev: std dev within each core, then average
            per_core_stds = g.groupby("core_id")["duration_ms"].std()
            per_core_vars = g.groupby("core_id")["duration_ms"].var()
            intra_core_std = per_core_stds.mean()
            intra_core_var = per_core_vars.mean()

            # Heterogeneity variance: total_var - intra_core_var
            # This represents variance from mixing different core types
            heterogeneity_var = max(0, total_var - intra_core_var)
            heterogeneity_std = np.sqrt(heterogeneity_var)

            # Per-core mean durations (to see P-core vs E-core difference)
            per_core_means = g.groupby("core_id")["duration_ms"].mean()

            rows.append({
                "parallelism": parallelism,
                "total_std_ms": round(total_std, 3),
                "intra_core_std_ms": round(intra_core_std, 3),
                "heterogeneity_std_ms": round(heterogeneity_std, 3),
                "heterogeneity_pct": round(100 * heterogeneity_var / total_var, 1) if total_var > 0 else 0,
                "core_mean_min_ms": round(per_core_means.min(), 2),
                "core_mean_max_ms": round(per_core_means.max(), 2),
                "core_mean_spread_ms": round(per_core_means.max() - per_core_means.min(), 2),
            })

        return pd.DataFrame(rows)

    variance_breakdown_df = _()
    return


@app.cell
def _(df):
    def _():
        # Compute duration for each inference
        df["duration_ms"] = (df["end_time"] - df["start_time"]).dt.total_seconds() * 1000

        # Classify cores: P-cores are 0-15, E-cores are 16-27
        df["core_type"] = df["core_id"].apply(lambda c: "P-core" if c < 16 else "E-core")

        # Compute mean and std dev by parallelism and core type
        rows = []
        for parallelism, g in df.groupby("parallelism"):
            for core_type, cg in g.groupby("core_type"):
                rows.append({
                    "parallelism": parallelism,
                    "core_type": core_type,
                    "mean_ms": cg["duration_ms"].mean(),
                    "std_ms": cg["duration_ms"].std(),
                    "count": len(cg),
                })

        return pd.DataFrame(rows)

    core_type_stats_df = _()
    return (core_type_stats_df,)


@app.cell
def _(core_type_stats_df):
    def _():
        fig, ax1 = plt.subplots(figsize=(10, 5))

        # Pivot for easier plotting
        p_core = core_type_stats_df[core_type_stats_df["core_type"] == "P-core"].set_index("parallelism")
        e_core = core_type_stats_df[core_type_stats_df["core_type"] == "E-core"].set_index("parallelism")

        # Left axis: Std dev
        ax1.plot(p_core.index, p_core["std_ms"], marker="o", color="#1a1a2e", label="P-core Std Dev", linewidth=2)
        if not e_core.empty:
            ax1.plot(e_core.index, e_core["std_ms"], marker="o", color="#e63946", label="E-core Std Dev", linewidth=2)
        ax1.set_xlabel("Parallelism")
        ax1.set_ylabel("Std Dev (ms)")
        ax1.tick_params(axis="y")
        ax1.set_ylim(bottom=0)

        # Right axis: Mean duration
        ax2 = ax1.twinx()
        ax2.plot(p_core.index, p_core["mean_ms"], marker="s", color="#1a1a2e", linestyle="--", label="P-core Mean", linewidth=2, alpha=0.7)
        if not e_core.empty:
            ax2.plot(e_core.index, e_core["mean_ms"], marker="s", color="#e63946", linestyle="--", label="E-core Mean", linewidth=2, alpha=0.7)
        ax2.set_ylabel("Mean Duration (ms)")
        ax2.tick_params(axis="y")
        ax2.set_ylim(bottom=0)

        ax1.axvline(x=9, color="gray", linestyle="--", alpha=0.7)
        ax1.text(9.1, ax1.get_ylim()[1] * 0.95, "E-cores\nenter", fontsize=9, color="gray", va="top")

        ax1.set_title("P-core vs E-core: Std Dev (solid) and Mean (dashed)")
        ax1.set_xticks(core_type_stats_df["parallelism"].unique())

        # Combined legend
        lines1, labels1 = ax1.get_legend_handles_labels()
        lines2, labels2 = ax2.get_legend_handles_labels()
        ax1.legend(lines1 + lines2, labels1 + labels2, loc="upper left")

        plt.tight_layout()
        return fig

    _()
    return


@app.cell
def _(df):
    def _():
        # Compute duration and core type
        df["duration_ms"] = (df["end_time"] - df["start_time"]).dt.total_seconds() * 1000
        df["core_type"] = df["core_id"].apply(lambda c: "P-core" if c < 16 else "E-core")

        # Compute throughput by parallelism and core type
        rows = []
        for parallelism, g in df.groupby("parallelism"):
            total_duration_s = (g["end_time"].max() - g["start_time"].min()).total_seconds()
            for core_type in ["P-core", "E-core"]:
                cg = g[g["core_type"] == core_type]
                throughput = len(cg) / total_duration_s if total_duration_s > 0 else 0
                rows.append({
                    "parallelism": parallelism,
                    "core_type": core_type,
                    "throughput": throughput,
                })

        throughput_by_core_df = pd.DataFrame(rows)

        # Pivot for stacking
        pivot = throughput_by_core_df.pivot(index="parallelism", columns="core_type", values="throughput").fillna(0)

        return pivot

    throughput_by_core_pivot = _()
    return (throughput_by_core_pivot,)


@app.cell
def _(throughput_by_core_pivot):
    def _():
        fig, ax = plt.subplots(figsize=(10, 5))

        parallelism = throughput_by_core_pivot.index.values
        p_core = throughput_by_core_pivot.get("P-core", pd.Series(0, index=parallelism)).values
        e_core = throughput_by_core_pivot.get("E-core", pd.Series(0, index=parallelism)).values

        ax.fill_between(parallelism, 0, p_core, label="P-core", color="#1a1a2e", alpha=0.8)
        ax.fill_between(parallelism, p_core, p_core + e_core, label="E-core", color="#e63946", alpha=0.8)

        ax.plot(parallelism, p_core + e_core, color="black", linewidth=1.5, linestyle="--", alpha=0.5)

        ax.axvline(x=9, color="white", linestyle="--", alpha=0.9, linewidth=1.5)
        ax.text(9.1, ax.get_ylim()[1] * 0.9, "E-cores\nenter", fontsize=9, color="white", va="top", fontweight="bold")

        ax.set_xlabel("Parallelism")
        ax.set_ylabel("Throughput (inferences/sec)")
        ax.set_title("Throughput by Core Type")
        ax.set_xticks(parallelism)
        ax.legend(loc="upper left")
        ax.set_ylim(bottom=0)
        ax.set_xlim(parallelism.min(), parallelism.max())

        plt.tight_layout()
        return fig

    _()
    return


@app.cell
def _(df):
    def _():
        # Get unique core_ids that appear in the data
        core_ids = sorted(df["core_id"].unique())
        return mo.ui.dropdown(
            options={str(c): c for c in core_ids},
            value=str(core_ids[0]),
            label="Select Core",
        )

    core_selector = _()
    return (core_selector,)


@app.cell
def _(core_selector, df):
    def _():
        selected_core = core_selector.value
        if selected_core is None:
            return None

        # Compute duration
        df["duration_ms"] = (df["end_time"] - df["start_time"]).dt.total_seconds() * 1000

        # Filter to selected core and compute stats per parallelism
        core_data = df[df["core_id"] == selected_core]

        rows = []
        for parallelism, g in core_data.groupby("parallelism"):
            rows.append({
                "parallelism": parallelism,
                "mean_ms": g["duration_ms"].mean(),
                "std_ms": g["duration_ms"].std(),
                "count": len(g),
            })

        if not rows:
            return mo.md(f"No data for core {selected_core}")

        stats = pd.DataFrame(rows)

        # Plot
        fig, ax1 = plt.subplots(figsize=(10, 5))

        # Left axis: Std dev
        color1 = "#1a1a2e"
        ax1.plot(stats["parallelism"], stats["std_ms"], marker="o", color=color1, linewidth=2, label="Std Dev")
        ax1.set_xlabel("Parallelism")
        ax1.set_ylabel("Std Dev (ms)", color=color1)
        ax1.tick_params(axis="y", labelcolor=color1)
        ax1.set_ylim(bottom=0)

        # Right axis: Mean
        ax2 = ax1.twinx()
        color2 = "#e63946"
        ax2.plot(stats["parallelism"], stats["mean_ms"], marker="s", color=color2, linewidth=2, label="Mean")
        ax2.set_ylabel("Mean Duration (ms)", color=color2)
        ax2.tick_params(axis="y", labelcolor=color2)
        ax2.set_ylim(bottom=0)

        core_type = "P-core" if selected_core < 16 else "E-core"
        ax1.set_title(f"Core {selected_core} ({core_type}): Mean and Std Dev by Parallelism")
        ax1.set_xticks(stats["parallelism"])

        # Combined legend
        lines1, labels1 = ax1.get_legend_handles_labels()
        lines2, labels2 = ax2.get_legend_handles_labels()
        ax1.legend(lines1 + lines2, labels1 + labels2, loc="upper left")

        plt.tight_layout()
        return fig

    cores_viz = _()
    return (cores_viz,)


@app.cell
def _(core_selector):
    core_selector
    return


@app.cell
def _(cores_viz):
    cores_viz
    return


@app.cell
def _(df):
    def _():
        core_ids = sorted(df["core_id"].unique())
        return mo.ui.dropdown(
            options={str(c): c for c in core_ids},
            value=str(core_ids[0]),
            label="Select Core (Raw)",
        )

    raw_core_selector = _()
    return (raw_core_selector,)


@app.cell
def _(df, raw_core_selector):
    def _():
        selected_core = raw_core_selector.value
        if selected_core is None:
            return None

        # Compute duration
        df["duration_ms"] = (df["end_time"] - df["start_time"]).dt.total_seconds() * 1000

        # Filter to selected core
        core_data = df[df["core_id"] == selected_core].copy()

        if core_data.empty:
            return mo.md(f"No data for core {selected_core}")

        # Add jitter to parallelism for visibility
        core_data["parallelism_jitter"] = core_data["parallelism"] + (np.random.rand(len(core_data)) - 0.5) * 0.3

        # Plot
        fig, ax = plt.subplots(figsize=(12, 5))

        ax.scatter(
            core_data["parallelism_jitter"],
            core_data["duration_ms"],
            alpha=0.3,
            s=10,
            color="#1a1a2e",
        )

        # Overlay mean line
        means = core_data.groupby("parallelism")["duration_ms"].mean()
        ax.plot(means.index, means.values, marker="o", color="#e63946", linewidth=2, label="Mean", zorder=10)

        ax.set_xlabel("Parallelism")
        ax.set_ylabel("Duration (ms)")
        ax.set_ylim(bottom=0)

        core_type = "P-core" if selected_core < 16 else "E-core"
        ax.set_title(f"Core {selected_core} ({core_type}): Raw Duration Measurements")
        ax.set_xticks(sorted(core_data["parallelism"].unique()))
        ax.legend(loc="upper left")

        plt.tight_layout()
        return fig

    raw_duration_viz = _()
    return (raw_duration_viz,)


@app.cell
def _(raw_core_selector):
    raw_core_selector
    return


@app.cell
def _(raw_duration_viz):
    raw_duration_viz
    return


@app.cell
def _(df):
    def _():
        # Compute duration
        df["duration_ms"] = (df["end_time"] - df["start_time"]).dt.total_seconds() * 1000

        # Filter to P-cores only (core_id < 16) and parallelism <= 8
        p_core_df = df[(df["core_id"] < 16) & (df["parallelism"] <= 8)]

        rows = []
        for parallelism, g in p_core_df.groupby("parallelism"):
            # Compute mean duration per core at this parallelism level
            core_means = g.groupby("core_id")["duration_ms"].mean()

            fastest_core = core_means.idxmin()
            slowest_core = core_means.idxmax()

            rows.append({
                "parallelism": parallelism,
                "fastest_core": fastest_core,
                "fastest_mean_ms": core_means[fastest_core],
                "slowest_core": slowest_core,
                "slowest_mean_ms": core_means[slowest_core],
                "spread_ms": core_means[slowest_core] - core_means[fastest_core],
            })

        stats = pd.DataFrame(rows)

        # Plot
        fig, ax = plt.subplots(figsize=(10, 5))

        ax.fill_between(
            stats["parallelism"],
            stats["fastest_mean_ms"],
            stats["slowest_mean_ms"],
            alpha=0.3,
            color="#1a1a2e",
            label="Spread",
        )
        ax.plot(stats["parallelism"], stats["fastest_mean_ms"], marker="v", color="#2ecc71", linewidth=2, label="Fastest P-core")
        ax.plot(stats["parallelism"], stats["slowest_mean_ms"], marker="^", color="#e74c3c", linewidth=2, label="Slowest P-core")

        # Annotate with core IDs
        for _, row in stats.iterrows():
            ax.annotate(f"core {int(row['fastest_core'])}", (row["parallelism"], row["fastest_mean_ms"]),
                        textcoords="offset points", xytext=(5, -10), fontsize=8, color="#2ecc71")
            ax.annotate(f"core {int(row['slowest_core'])}", (row["parallelism"], row["slowest_mean_ms"]),
                        textcoords="offset points", xytext=(5, 5), fontsize=8, color="#e74c3c")

        ax.set_xlabel("Parallelism")
        ax.set_ylabel("Mean Duration (ms)")
        ax.set_title("P-core Fastest vs Slowest Mean Duration (Parallelism 1-8)")
        ax.set_xticks(stats["parallelism"])
        ax.legend()
        ax.set_ylim(bottom=0)

        plt.tight_layout()

        max_spread = stats["spread_ms"].max()
        max_spread_row = stats.loc[stats["spread_ms"].idxmax()]
        summary = mo.md(f"**Maximum P-core spread:** {max_spread:.1f}ms at parallelism {int(max_spread_row['parallelism'])} (core {int(max_spread_row['fastest_core'])} vs core {int(max_spread_row['slowest_core'])})")

        return mo.vstack([fig, summary])

    _()
    return


if __name__ == "__main__":
    app.run()
