#!/usr/bin/env -S uv run marimo edit --no-token --no-skew-protection --watch --port 3007

import marimo

__generated_with = "0.23.3"
app = marimo.App(width="medium")

with app.setup:
    import marimo as mo
    import pandas as pd
    import seaborn as sns
    import matplotlib.pyplot as plt
    from bench import build, run_inference

    sns.set_theme()


@app.cell
def _():
    build()
    return


@app.cell
def _():
    # Even-numbered P-core hyperthreads (one per physical P-core) followed by
    # all E-cores. Order matches topo.svg.
    cores = [0, 2, 4, 6, 8, 10, 12, 14] + list(range(16, 28))
    return (cores,)


@app.cell
def _(cores):
    # Sweep divider d over [-1, N-1]: cores[:d+1] run DbNet, cores[d+1:] run SVTR.
    # d=-1 → 0 detection cores; d=N-1 → 0 recognition cores.
    dividers = list(range(-1, len(cores)))
    configs = [
        [("dbnet", c) for c in cores[: d + 1]]
        + [("svtr", c) for c in cores[d + 1 :]]
        for d in dividers
    ]
    df = run_inference(configs=configs, duration=4, trim=1.0)
    return df, dividers


@app.cell
def _(df, dividers):
    def _():
        per = (
            df.groupby(["config_idx", "model"])
            .apply(
                lambda g: pd.Series({
                    "throughput": len(g) / (g["end_mono"].max() - g["start_mono"].min()),
                }),
                include_groups=False,
            )
            .reset_index()
        )
        wide = (
            per.pivot(index="config_idx", columns="model", values="throughput")
            .rename_axis(columns=None)
            .reset_index()
        )
        # Endpoints with 0 cores on a side produce no rows for that model;
        # pivot leaves those cells NaN — fill with 0.
        for col in ("dbnet", "svtr"):
            if col not in wide.columns:
                wide[col] = 0.0
        wide[["dbnet", "svtr"]] = wide[["dbnet", "svtr"]].fillna(0.0)
        wide["divider"] = wide["config_idx"].map(lambda i: dividers[i])
        return wide.sort_values("divider").reset_index(drop=True)

    stats = _()
    return (stats,)


@app.cell
def _(stats):
    def _():
        x = stats["divider"] + 1

        fig, ax1 = plt.subplots(figsize=(10, 6))

        rec_color = "#e76f51"
        ax1.plot(
            x, stats["svtr"],
            marker="s", color=rec_color, linewidth=2, label="Recognition (SVTR)",
        )
        ax1.set_xlabel("Detection threads")
        ax1.set_ylabel("Recognition Inferences / sec", color=rec_color)
        ax1.tick_params(axis="y", labelcolor=rec_color)
        ax1.set_ylim(bottom=0)
        ax1.set_xticks(range(int(x.min()), int(x.max()) + 1))

        ax2 = ax1.twinx()
        det_color = "#264653"
        ax2.plot(
            x, stats["dbnet"],
            marker="o", color=det_color, linewidth=2, label="Detection (DbNet)",
        )
        ax2.set_ylabel("Detection Inferences / sec", color=det_color)
        ax2.tick_params(axis="y", labelcolor=det_color)
        ax2.set_ylim(bottom=0)

        ax1.set_title("Detection vs Recognition Throughput by Detection Threads")

        lines1, labels1 = ax1.get_legend_handles_labels()
        lines2, labels2 = ax2.get_legend_handles_labels()
        ax1.legend(
            lines1 + lines2, labels1 + labels2,
            loc="upper center",
            bbox_to_anchor=(0.5, -0.1),
            ncol=2,
            frameon=True,
        )

        plt.tight_layout()
        return fig

    _()
    return


if __name__ == "__main__":
    app.run()
