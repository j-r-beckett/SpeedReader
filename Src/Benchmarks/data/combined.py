import marimo

__generated_with = "0.16.5"
app = marimo.App(width="full")


@app.cell
def _():
    import marimo as mo
    import polars as pl
    import altair as alt

    # Parse the data into a polars DataFrame
    _data = """
    0        640x640      1                          7.35                 7.73
    0        640x640      2                         13.36                15.15
    0        640x640      3                         16.65                20.10
    0        640x640      4                         20.21                25.00
    0        640x640      5                         22.37                28.23
    0        640x640      6                         23.90                30.75
    0        640x640      7                         24.28                31.82
    0        640x640      8                         24.43                32.36
    1        640x640      1                          7.11                10.57
    1        640x640      2                         13.06                17.88
    1        640x640      3                         17.07                23.47
    1        640x640      4                         19.74                27.05
    1        640x640      5                         20.58                28.65
    1        640x640      6                         22.76                31.47
    1        640x640      7                         23.19                32.26
    1        640x640      8                         22.97                32.12
    2        640x640      1                          6.81                12.16
    2        640x640      2                         12.08                19.04
    2        640x640      3                         16.31                24.95
    2        640x640      4                         18.24                27.07
    2        640x640      5                         20.83                30.78
    2        640x640      6                         21.43                31.41
    2        640x640      7                         21.65                31.76
    2        640x640      8                         22.44                32.18
    3        640x640      1                          6.67                11.89
    3        640x640      2                         12.28                19.64
    3        640x640      3                         15.82                24.46
    3        640x640      4                         18.40                28.03
    3        640x640      5                         19.94                30.06
    3        640x640      6                         20.96                31.34
    3        640x640      7                         21.08                31.43
    3        640x640      8                         21.36                31.91
    4        640x640      1                          6.66                12.28
    4        640x640      2                         11.78                19.74
    4        640x640      3                         15.12                24.17
    4        640x640      4                         18.25                28.67
    4        640x640      5                         19.44                30.16
    4        640x640      6                         19.78                30.62
    4        640x640      7                         19.74                31.01
    4        640x640      8                         20.58                31.63
    5        640x640      1                          6.45                12.73
    5        640x640      2                         11.38                19.92
    5        640x640      3                         14.37                24.00
    5        640x640      4                         17.60                28.14
    5        640x640      5                         17.93                29.12
    5        640x640      6                         18.87                30.34
    5        640x640      7                         19.48                30.91
    5        640x640      8                         19.94                31.46
    6        640x640      1                          6.27                13.08
    6        640x640      2                         10.98                20.16
    6        640x640      3                         14.13                24.16
    6        640x640      4                         15.77                26.67
    6        640x640      5                         16.27                27.76
    6        640x640      6                         17.65                29.40
    6        640x640      7                         18.22                30.32
    6        640x640      8                         18.93                31.16
    7        640x640      1                          5.99                13.62
    7        640x640      2                         10.64                20.34
    7        640x640      3                         12.44                23.25
    7        640x640      4                         14.24                26.02
    7        640x640      5                         16.08                28.06
    7        640x640      6                         16.81                29.11
    7        640x640      7                         17.40                29.90
    7        640x640      8                         18.31                31.05
    8        640x640      1                          5.97                14.15
    8        640x640      2                          9.97                20.03
    8        640x640      3                         12.95                24.24
    8        640x640      4                         12.63                24.52
    8        640x640      5                         15.47                27.80
    8        640x640      6                         16.77                29.38
    8        640x640      7                         17.09                30.18
    8        640x640      8                         17.22                30.44
    9        640x640      1                          4.03                12.21
    9        640x640      2                          7.75                17.90
    9        640x640      3                         11.56                23.21
    9        640x640      4                         13.86                26.33
    9        640x640      5                         13.58                26.66
    9        640x640      6                         16.19                29.37
    9        640x640      7                         14.89                28.83
    9        640x640      8                         16.12                30.15
    10       640x640      1                          4.31                13.25
    10       640x640      2                          7.23                17.76
    10       640x640      3                          8.94                20.36
    10       640x640      4                         13.31                26.06
    10       640x640      5                         12.62                26.66
    10       640x640      6                         13.09                27.31
    10       640x640      7                         15.78                29.74
    10       640x640      8                         15.68                30.09
    """


    MAX_NOISE_LEVEL = 7

    rows = []
    for line in _data.strip().split('\n'):
        parts = line.split()
        noise = int(parts[0])
        # Filter out noise levels above threshold
        if noise <= MAX_NOISE_LEVEL:
            rows.append({
                'noise': noise,
                'size': parts[1],
                'threads': int(parts[2]),
                'throughput': float(parts[3]),
                'memory_bw': float(parts[4])
            })

    df = pl.DataFrame(rows)
    df
    return MAX_NOISE_LEVEL, alt, df, mo, pl


@app.cell
def _(MAX_NOISE_LEVEL, mo):
    # Create a range slider to select noise levels
    noise_range = mo.ui.range_slider(
        start=0,
        stop=MAX_NOISE_LEVEL,
        value=[0, MAX_NOISE_LEVEL],
        step=1,
        label="Noise Level Range",
        full_width=True
    )
    noise_range
    return (noise_range,)


@app.cell
def _(alt, df, mo, noise_range, pl):
    # Filter data based on slider selection and create charts
    filtered_df = df.filter(
        (pl.col('noise') >= noise_range.value[0]) & 
        (pl.col('noise') <= noise_range.value[1])
    )

    throughput_chart = alt.Chart(filtered_df).mark_line(point=True).encode(
        x=alt.X('threads:Q', title='Threads', scale=alt.Scale(domain=[1, 8])),
        y=alt.Y('throughput:Q', title='Throughput (inferences/sec)'),
        color=alt.Color('noise:N', title='Noise Level', scale=alt.Scale(scheme='tableau10')),
        tooltip=[
            alt.Tooltip('noise:N', title='Noise'),
            alt.Tooltip('threads:Q', title='Threads'),
            alt.Tooltip('throughput:Q', title='Throughput', format='.2f'),
            alt.Tooltip('memory_bw:Q', title='Memory BW', format='.2f')
        ]
    ).properties(
        width=700,
        height=400,
        title='Throughput vs Thread Count by Noise Level'
    )

    memory_bw_chart = alt.Chart(filtered_df).mark_line(point=True).encode(
        x=alt.X('threads:Q', title='Threads', scale=alt.Scale(domain=[1, 8])),
        y=alt.Y('memory_bw:Q', title='Memory Bandwidth (GB/s)'),
        color=alt.Color('noise:N', title='Noise Level', scale=alt.Scale(scheme='tableau10')),
        tooltip=[
            alt.Tooltip('noise:N', title='Noise'),
            alt.Tooltip('threads:Q', title='Threads'),
            alt.Tooltip('throughput:Q', title='Throughput', format='.2f'),
            alt.Tooltip('memory_bw:Q', title='Memory BW', format='.2f')
        ]
    ).properties(
        width=700,
        height=400,
        title='Memory Bandwidth vs Thread Count by Noise Level'
    )

    mo.vstack([
        mo.md(f"""
        ## Throughput Analysis by Noise Level
    
        **Selected range:** Noise levels {noise_range.value[0]} to {noise_range.value[1]}
        """),
        noise_range,
        throughput_chart,
        memory_bw_chart
    ])
    return


if __name__ == "__main__":
    app.run()
