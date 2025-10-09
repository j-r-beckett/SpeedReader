import marimo

__generated_with = "0.16.5"
app = marimo.App(width="full")


@app.cell
def _():
    import marimo as mo
    import polars as pl
    import altair as alt

    data = pl.DataFrame({
        "threads": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
        "throughput": [7.34, 13.63, 17.18, 20.42, 23.01, 24.19, 24.37, 24.67, 24.48, 24.46, 24.25, 24.02],
        "bandwidth": [7.44, 15.01, 21.18, 25.23, 29.24, 30.99, 31.65, 32.24, 32.31, 32.45, 32.42, 32.36]
    })
    return alt, data, mo, pl


@app.cell
def _(alt, data):
    def _():
        bandwidth_chart = alt.Chart(data).mark_line(color='steelblue', point=True).encode(
            x=alt.X('threads:Q', title='Threads'),
            y=alt.Y('bandwidth:Q', title='Memory Bandwidth (GB/s)'),
            tooltip=[
                alt.Tooltip('threads:Q', title='Threads'),
                alt.Tooltip('bandwidth:Q', title='Memory Bandwidth (GB/s)')
            ]
        ).properties(width=600, height=200, title='Memory Bandwidth')

        throughput_chart = alt.Chart(data).mark_line(color='orange', point=True).encode(
            x=alt.X('threads:Q', title='Threads'),
            y=alt.Y('throughput:Q', title='Throughput (tiles/sec)'),
            tooltip=[
                alt.Tooltip('threads:Q', title='Threads'),
                alt.Tooltip('throughput:Q', title='Throughput (tiles/sec)')
            ]
        ).properties(width=600, height=200, title='Throughput')

        return bandwidth_chart & throughput_chart

    _()
    return


@app.cell
def _(alt, data, mo, pl):
    def _():
        from scipy.optimize import curve_fit
        import numpy as np
    
        # Logistic function
        def logistic(x, L, k, x0):
            return L / (1 + np.exp(-k * (x - x0)))
    
        # Fit model
        threads_data = data['threads'].to_numpy()
        bandwidth_values = data['bandwidth'].to_numpy()
        (L_fit, k_fit, x0_fit), _ = curve_fit(logistic, threads_data, bandwidth_values, p0=[35, 1, 5])
    
        # Calculate R^2
        predictions = logistic(threads_data, L_fit, k_fit, x0_fit)
        r_squared = 1 - np.sum((bandwidth_values - predictions)**2) / np.sum((bandwidth_values - bandwidth_values.mean())**2)
    
        # Create smooth prediction curve
        threads_smooth = np.linspace(1, 12, 100)
        pred_df = pl.DataFrame({
            'threads': threads_smooth,
            'predicted_bandwidth': logistic(threads_smooth, L_fit, k_fit, x0_fit)
        })
    
        # Visualization
        observed_logistic = alt.Chart(data).mark_circle(size=100, color='steelblue').encode(
            x=alt.X('threads:Q', title='Threads'),
            y=alt.Y('bandwidth:Q', title='Memory Bandwidth (GB/s)'),
            tooltip=['threads:Q', 'bandwidth:Q']
        )
    
        fitted_logistic = alt.Chart(pred_df).mark_line(color='orange', strokeWidth=2).encode(
            x='threads:Q',
            y='predicted_bandwidth:Q'
        )
    
        logistic_chart = (observed_logistic + fitted_logistic).properties(
            width=600, height=400, title='Thread Count vs Memory Bandwidth'
        )
    
        # Summary
        logistic_summary = mo.md(f"""
        ## Thread Count -> Bandwidth
    
        **Logistic Best Fit**
    
        `bandwidth = {L_fit:.3f} / (1 + exp(-{k_fit:.3f} * (threads - {x0_fit:.3f})))`
    
        - `L` = {L_fit:.3f} (asymptote, predicted maximum bandwidth)
        - `k` = {k_fit:.3f} (steepness, no clear physical interpretation)
        - `x_0` = {x0_fit:.3f} (point of maximum growth rate)
    
        **Correctness**
    
        - `R²` = {r_squared:.4f}
        """)
    
        # Return visualization and parameters
        return mo.vstack([logistic_chart, logistic_summary]), (L_fit, k_fit, x0_fit, logistic)

    logistic_viz, logistic_model = _()
    logistic_viz
    return (logistic_model,)


@app.cell
def _(alt, data, mo, pl):
    def _():
        from scipy import stats
        import numpy as np
    
        # Extract bandwidth and throughput data
        bandwidth_data = data['bandwidth'].to_numpy()
        throughput_data = data['throughput'].to_numpy()
    
        # Perform linear regression
        slope, intercept, r_value, p_value, std_err = stats.linregress(bandwidth_data, throughput_data)
        r_squared = r_value**2
    
        # Create smooth prediction curve
        bandwidth_smooth = np.linspace(bandwidth_data.min(), bandwidth_data.max(), 100)
        throughput_pred = intercept + slope * bandwidth_smooth
    
        pred_df = pl.DataFrame({
            'bandwidth': bandwidth_smooth,
            'predicted_throughput': throughput_pred
        })
    
        # Visualization
        observed_linear = alt.Chart(data).mark_circle(size=100, color='steelblue').encode(
            x=alt.X('bandwidth:Q', title='Memory Bandwidth (GB/s)'),
            y=alt.Y('throughput:Q', title='Throughput (tiles/sec)'),
            tooltip=['bandwidth:Q', 'throughput:Q']
        )
    
        fitted_linear = alt.Chart(pred_df).mark_line(color='orange', strokeWidth=2).encode(
            x='bandwidth:Q',
            y='predicted_throughput:Q'
        )
    
        linear_chart = (observed_linear + fitted_linear).properties(
            width=600, height=400, title='Memory Bandwidth vs Throughput'
        )
    
        # Summary
        linear_summary = mo.md(f"""
        ## Bandwidth -> Throughput
    
        **Linear Best Fit**
    
        `throughput = {intercept:.3f} + {slope:.3f} × bandwidth`
    
        - `slope` = {slope:.3f} tiles/sec per GB/s
        - `intercept` = {intercept:.3f} tiles/sec
    
        **Correctness**
    
        - `R²` = {r_squared:.4f}

        **Hypothesis:** `1/slope = 1/[(tiles/sec) / (GB/s)] = GB/tile = dbnet_memory_intensity` is a function of the model, the input size, and the execution provider, and so `slope` will remain constant across machines. Needs confirmation!
        """)
    
        # Return visualization and parameters
        return mo.vstack([linear_chart, linear_summary]), (slope, intercept)

    linear_viz, linear_model = _()
    linear_viz
    return (linear_model,)


@app.cell
def _(alt, data, linear_model, logistic_model, mo, pl):
    def _():
        import numpy as np
    
        # Unpack model parameters
        L, k, x0, logistic_func = logistic_model
        slope, intercept = linear_model
    
        # Composed function: throughput = linear(logistic(threads))
        def composed_model(threads):
            bandwidth = logistic_func(threads, L, k, x0)
            throughput = intercept + slope * bandwidth
            return throughput
    
        # Get observed data
        threads_obs = data['threads'].to_numpy()
        throughput_obs = data['throughput'].to_numpy()
    
        # Calculate predictions and R^2
        throughput_pred = composed_model(threads_obs)
        ss_res = np.sum((throughput_obs - throughput_pred)**2)
        ss_tot = np.sum((throughput_obs - throughput_obs.mean())**2)
        r_squared_composed = 1 - (ss_res / ss_tot)
    
        # Create smooth prediction curve
        threads_smooth = np.linspace(1, 12, 100)
        composed_pred_df = pl.DataFrame({
            'threads': threads_smooth,
            'predicted_throughput': composed_model(threads_smooth)
        })
    
        # Visualization
        observed_composed = alt.Chart(data).mark_circle(size=100, color='steelblue').encode(
            x=alt.X('threads:Q', title='Threads'),
            y=alt.Y('throughput:Q', title='Throughput (tiles/sec)'),
            tooltip=['threads:Q', 'throughput:Q']
        )
    
        fitted_composed = alt.Chart(composed_pred_df).mark_line(color='orange', strokeWidth=2).encode(
            x='threads:Q',
            y='predicted_throughput:Q'
        )
    
        composed_chart = (observed_composed + fitted_composed).properties(
            width=600, height=400, title='Thread Count vs Throughput (Two-Stage Model)'
        )
    
        # Summary
        composed_summary = mo.md(f"""
        ## Thread Count -> Throughput
    
        **Model**
    
        `throughput = {intercept:.3f} + 1 / dbnet_memory_intensity * max_bandwidth / (1 + exp(-{k:.3f} * (threads - {x0:.3f})))` where `dbnet_memory_intensity = {slope:.3f}` (constant) and `max_bandwidth = {L:.3f}`
    
        This is the function composition `throughput = g(f(threads))` where:
        - `f(threads)` = logistic model for bandwidth
        - `g(bandwidth)` = linear model for throughput
    
        **Correctness**
    
        - `R²` = {r_squared_composed:.4f}
        """)
    
        return mo.vstack([composed_chart, composed_summary])

    _()
    return


if __name__ == "__main__":
    app.run()
