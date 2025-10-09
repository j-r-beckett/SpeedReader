import marimo

__generated_with = "0.16.5"
app = marimo.App(width="full")


@app.cell
def _():
    import marimo as mo
    import polars as pl
    import altair as alt

    # Create dataframe with the new data
    data = pl.DataFrame({
        "Threads": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16],
        "Throughput (inferences/sec)": [105.91, 209.45, 308.99, 413.08, 499.42, 583.86, 658.62, 713.60, 752.16, 789.44, 824.74, 849.15, 880.98, 908.41, 933.55, 964.93],
        "Memory BW (GB/s)": [1.96, 2.46, 3.07, 3.67, 3.90, 4.69, 5.41, 5.73, 6.10, 6.59, 7.33, 7.74, 8.26, 9.17, 9.79, 10.67]
    })

    # Create Memory Bandwidth chart
    bandwidth_chart = alt.Chart(data).mark_line(color='steelblue', point=True).encode(
        x=alt.X('Threads:Q', title='Threads'),
        y=alt.Y('Memory BW (GB/s):Q', title='Memory BW (GB/s)'),
        tooltip=['Threads', 'Memory BW (GB/s)']
    ).properties(
        width=600, 
        height=200, 
        title='Memory Bandwidth'
    )

    # Create Throughput chart
    throughput_chart = alt.Chart(data).mark_line(color='orange', point=True).encode(
        x=alt.X('Threads:Q', title='Threads'),
        y=alt.Y('Throughput (inferences/sec):Q', title='Throughput (inferences/sec)'),
        tooltip=['Threads', 'Throughput (inferences/sec)']
    ).properties(
        width=600, 
        height=200, 
        title='Throughput'
    )

    # Stack the charts vertically
    combined_chart = bandwidth_chart & throughput_chart
    combined_chart
    return alt, data, mo, pl


@app.cell
def _(alt, data, mo, pl):
    from scipy import stats

    # Extract bandwidth and throughput data
    bandwidth_data = data['Memory BW (GB/s)'].to_list()
    throughput_data = data['Throughput (inferences/sec)'].to_list()

    # Perform linear regression
    slope, intercept, r_value, p_value, std_err = stats.linregress(bandwidth_data, throughput_data)
    r_squared_linear = r_value**2

    # Generate smooth line for visualization
    bandwidth_smooth = [min(bandwidth_data) + (max(bandwidth_data) - min(bandwidth_data)) * i / 99 for i in range(100)]
    throughput_pred = [intercept + slope * bw for bw in bandwidth_smooth]

    # Create visualization
    observed_linear = alt.Chart(data).mark_circle(size=100, color='steelblue').encode(
        x=alt.X('Memory BW (GB/s):Q', title='Memory BW (GB/s)'),
        y=alt.Y('Throughput (inferences/sec):Q', title='Throughput (inferences/sec)'),
        tooltip=['Memory BW (GB/s)', 'Throughput (inferences/sec)']
    )

    pred_df_linear = pl.DataFrame({
        'Memory BW (GB/s)': bandwidth_smooth,
        'Predicted Throughput': throughput_pred
    })

    fitted_linear = alt.Chart(pred_df_linear).mark_line(color='orange', strokeWidth=2).encode(
        x='Memory BW (GB/s):Q',
        y=alt.Y('Predicted Throughput:Q', title='Throughput (inferences/sec)'),
        tooltip=['Memory BW (GB/s)', 'Predicted Throughput']
    )

    linear_chart = (observed_linear + fitted_linear).properties(
        width=600,
        height=400,
        title='Linear Regression: Throughput vs Memory Bandwidth'
    )

    # Create summary
    linear_summary = mo.md(f"""
    ## Throughput vs Bandwidth (Linear)

    **Model Equation:**
    `Throughput = {intercept:.3f} + {slope:.3f} × Bandwidth`

    **Fitted Parameters:**
    - **Slope:** {slope:.3f} inferences/sec per GB/s
    - **Intercept:** {intercept:.3f} inferences/sec

    **Model Performance:**
    - **R² score:** {r_squared_linear:.4f}
    - **P-value:** {p_value:.2e}
    - **Standard error:** {std_err:.3f}

    **Interpretation:**
    - For every **1 GB/s** increase in memory bandwidth, throughput increases by **{slope:.3f} inferences/sec**
    - The model explains **{r_squared_linear*100:.2f}%** of the variance in throughput
    - The strong linear relationship suggests throughput is largely bandwidth-bound
    """)

    mo.vstack([linear_chart, linear_summary])
    return


if __name__ == "__main__":
    app.run()
