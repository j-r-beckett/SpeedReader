import marimo

__generated_with = "0.16.5"
app = marimo.App(width="full")


@app.cell
def _():
    import marimo as mo
    import polars as pl
    import altair as alt

    data = pl.DataFrame({
        "Threads": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16],
        "Throughput (inferences/sec)": [105.91, 209.45, 308.99, 413.08, 499.42, 583.86, 658.62, 713.60, 752.16, 789.44, 824.74, 849.15, 880.98, 908.41, 933.55, 964.93],
        "Memory BW (GB/s)": [1.96, 2.46, 3.07, 3.67, 3.90, 4.69, 5.41, 5.73, 6.10, 6.59, 7.33, 7.74, 8.26, 9.17, 9.79, 10.67]
    })
    return alt, data, mo, pl


@app.cell
def _(alt, data):
    def _():
        bandwidth_chart = alt.Chart(data).mark_line(color='steelblue', point=True).encode(
            x=alt.X('Threads:Q', title='Threads'),
            y=alt.Y('Memory BW (GB/s):Q', title='Memory BW (GB/s)'),
            tooltip=['Threads', 'Memory BW (GB/s)']
        ).properties(
            width=600, 
            height=200, 
            title='Memory Bandwidth'
        )

        throughput_chart = alt.Chart(data).mark_line(color='orange', point=True).encode(
            x=alt.X('Threads:Q', title='Threads'),
            y=alt.Y('Throughput (inferences/sec):Q', title='Throughput (inferences/sec)'),
            tooltip=['Threads', 'Throughput (inferences/sec)']
        ).properties(
            width=600, 
            height=200, 
            title='Throughput'
        )

        return bandwidth_chart & throughput_chart
    _()
    return


@app.cell
def _(alt, data, mo, pl):
    def _():
        from scipy import stats
        import numpy as np

        # Extract thread and bandwidth data
        threads_data = data['Threads'].to_numpy()
        bandwidth_data = data['Memory BW (GB/s)'].to_numpy()

        # Perform linear regression
        slope, intercept, r_value, p_value, std_err = stats.linregress(threads_data, bandwidth_data)
        r_squared = r_value**2

        # Create smooth prediction curve
        threads_smooth = np.linspace(threads_data.min(), threads_data.max(), 100)
        bandwidth_pred = intercept + slope * threads_smooth

        pred_df = pl.DataFrame({
            'threads': threads_smooth,
            'predicted_bandwidth': bandwidth_pred
        })

        # Visualization
        observed_points = alt.Chart(data).mark_circle(size=100, color='steelblue').encode(
            x=alt.X('Threads:Q', title='Threads'),
            y=alt.Y('Memory BW (GB/s):Q', title='Memory Bandwidth (GB/s)'),
            tooltip=['Threads:Q', 'Memory BW (GB/s):Q']
        )

        fitted_line = alt.Chart(pred_df).mark_line(color='orange', strokeWidth=2).encode(
            x='threads:Q',
            y='predicted_bandwidth:Q'
        )

        regression_chart = (observed_points + fitted_line).properties(
            width=600, height=400, title='Thread Count vs Memory Bandwidth'
        )

        # Summary
        regression_summary = mo.md(f"""
        ## Thread Count -> Bandwidth

        **Linear Best Fit**

        `bandwidth = {intercept:.3f} + {slope:.3f} × threads`

        - `slope` = {slope:.3f} GB/s per thread
        - `intercept` = {intercept:.3f} GB/s

        **Correctness**

        - `R²` = {r_squared:.4f}
        """)

        return mo.vstack([regression_chart, regression_summary]), (slope, intercept)

    bandwidth_regression_viz, bandwidth_regression_model = _()
    bandwidth_regression_viz
    return


@app.cell
def _(alt, data, mo, pl):
    def _():
        from scipy.optimize import curve_fit
        import numpy as np

        # Exponential saturation function
        def exp_saturation(x, a, b):
            return a * (1 - np.exp(-b * x))

        # Extract thread and throughput data
        threads_data = data['Threads'].to_numpy()
        throughput_data = data['Throughput (inferences/sec)'].to_numpy()

        # Fit model
        (a_fit, b_fit), _ = curve_fit(exp_saturation, threads_data, throughput_data, p0=[1000, 0.1])

        # Calculate R²
        predictions = exp_saturation(threads_data, a_fit, b_fit)
        r_squared = 1 - np.sum((throughput_data - predictions)**2) / np.sum((throughput_data - throughput_data.mean())**2)

        # Create smooth prediction curve
        threads_smooth = np.linspace(threads_data.min(), threads_data.max(), 100)
        pred_df = pl.DataFrame({
            'threads': threads_smooth,
            'predicted_throughput': exp_saturation(threads_smooth, a_fit, b_fit)
        })

        # Visualization
        observed_points = alt.Chart(data).mark_circle(size=100, color='steelblue').encode(
            x=alt.X('Threads:Q', title='Threads'),
            y=alt.Y('Throughput (inferences/sec):Q', title='Throughput (inferences/sec)'),
            tooltip=['Threads:Q', 'Throughput (inferences/sec):Q']
        )

        fitted_curve = alt.Chart(pred_df).mark_line(color='orange', strokeWidth=2).encode(
            x='threads:Q',
            y='predicted_throughput:Q'
        )

        saturation_chart = (observed_points + fitted_curve).properties(
            width=600, height=400, title='Thread Count vs Throughput'
        )

        # Summary
        saturation_summary = mo.md(f"""
        ## Thread Count -> Throughput

        **Exponential Saturation Best Fit**

        `throughput = {a_fit:.3f} × (1 - exp(-{b_fit:.3f} × threads))`

        - `a` = {a_fit:.3f} inferences/sec (asymptotic maximum throughput)
        - `b` = {b_fit:.3f} (saturation rate constant)

        **Correctness**

        - `R²` = {r_squared:.4f}
        """)

        return mo.vstack([saturation_chart, saturation_summary]), (a_fit, b_fit, exp_saturation)

    throughput_thread_saturation_viz, throughput_thread_saturation_model = _()
    throughput_thread_saturation_viz
    return


@app.cell
def _(data, mo):
    # Create a slider to select number of points for fitting
    n_points_slider = mo.ui.slider(
        start=3, 
        stop=len(data), 
        value=len(data), 
        label="Number of points to use for fitting",
        step=1
    )
    return (n_points_slider,)


@app.cell
def _(alt, data, mo, n_points_slider, pl):
    from scipy.optimize import curve_fit
    import numpy as np

    # Exponential saturation function
    def exp_saturation(x, a, b):
        return a * (1 - np.exp(-b * x))

    # Extract thread and throughput data
    threads_data = data['Threads'].to_numpy()
    throughput_data = data['Throughput (inferences/sec)'].to_numpy()

    # Use only first n points for fitting
    n = n_points_slider.value
    threads_fit = threads_data[:n]
    throughput_fit = throughput_data[:n]

    # Fit model using only first n points
    (a_fit, b_fit), _ = curve_fit(exp_saturation, threads_fit, throughput_fit, p0=[1000, 0.1])

    # Calculate R^2 on fitting data
    predictions_fit = exp_saturation(threads_fit, a_fit, b_fit)
    r_squared = 1 - np.sum((throughput_fit - predictions_fit)**2) / np.sum((throughput_fit - throughput_fit.mean())**2)

    # Create smooth prediction curve across all thread values
    threads_smooth = np.linspace(threads_data.min(), threads_data.max(), 100)
    pred_df = pl.DataFrame({
        'threads': threads_smooth,
        'predicted_throughput': exp_saturation(threads_smooth, a_fit, b_fit)
    })

    # Split data into fitted and unfitted points
    fitted_data = data[:n]
    unfitted_data = data[n:]

    # Visualization
    fitted_points = alt.Chart(fitted_data).mark_circle(size=100, color='steelblue').encode(
        x=alt.X('Threads:Q', title='Threads'),
        y=alt.Y('Throughput (inferences/sec):Q', title='Throughput (inferences/sec)'),
        tooltip=['Threads:Q', 'Throughput (inferences/sec):Q']
    )

    unfitted_points = alt.Chart(unfitted_data).mark_circle(size=100, color='lightgray', opacity=0.5).encode(
        x=alt.X('Threads:Q'),
        y=alt.Y('Throughput (inferences/sec):Q'),
        tooltip=['Threads:Q', 'Throughput (inferences/sec):Q']
    )

    fitted_curve = alt.Chart(pred_df).mark_line(color='orange', strokeWidth=2).encode(
        x='threads:Q',
        y='predicted_throughput:Q'
    )

    interactive_saturation_chart = (fitted_points + unfitted_points + fitted_curve).properties(
        width=600, height=400, 
        title=f'Thread Count vs Throughput (fitted on first {n} points)'
    )

    # Summary
    interactive_saturation_summary = mo.md(f"""
    ## Thread Count → Throughput (First {n} Points)

    **Exponential Saturation Best Fit**

    `throughput = {a_fit:.3f} × (1 - exp(-{b_fit:.3f} × threads))`

    - `a` = {a_fit:.3f} inferences/sec (asymptotic maximum throughput)
    - `b` = {b_fit:.3f} (saturation rate constant)

    **Correctness (on fitted data)**

    - `R²` = {r_squared:.4f}

    **Legend:**
    - 🔵 Blue points: Used for fitting
    - ⚪ Gray points: Not used for fitting
    - 🟠 Orange line: Fitted curve (extrapolated across all threads)
    """)

    # Create controls sidebar
    controls = mo.vstack([
        n_points_slider,
        interactive_saturation_summary
    ])

    # Put slider and summary next to the chart
    mo.hstack([interactive_saturation_chart, controls], widths=[2, 1])
    return


if __name__ == "__main__":
    app.run()
