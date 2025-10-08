import marimo

__generated_with = "0.16.5"
app = marimo.App(width="full")


@app.cell
def _():
    import marimo as mo
    import polars as pl
    import altair as alt

    df = pl.DataFrame({
        "Threads": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
        "Throughput (tiles/sec)": [7.34, 13.63, 17.18, 20.42, 23.01, 24.19, 24.37, 24.67, 24.48, 24.46, 24.25, 24.02],
        "Memory Bandwidth (GB/s)": [7.44, 15.01, 21.18, 25.23, 29.24, 30.99, 31.65, 32.24, 32.31, 32.45, 32.42, 32.36]
    })

    bandwidth = alt.Chart(df).mark_line(color='steelblue', point=True).encode(
        x=alt.X('Threads:Q', title='Threads'),
        y=alt.Y('Memory Bandwidth (GB/s):Q'),
        tooltip=['Threads', 'Memory Bandwidth (GB/s)']
    ).properties(width=600, height=200, title='Memory Bandwidth')

    throughput = alt.Chart(df).mark_line(color='orange', point=True).encode(
        x=alt.X('Threads:Q', title='Threads'),
        y=alt.Y('Throughput (tiles/sec):Q'),
        tooltip=['Threads', 'Throughput (tiles/sec)']
    ).properties(width=600, height=200, title='Throughput')

    chart = bandwidth & throughput
    chart
    return alt, df, mo, pl


@app.cell
def _(alt, df, mo, pl):
    from scipy import stats

    # Extract bandwidth and throughput data
    bandwidth_data = df['Memory Bandwidth (GB/s)'].to_list()
    throughput_data = df['Throughput (tiles/sec)'].to_list()

    # Perform linear regression
    slope, intercept, r_value, p_value, std_err = stats.linregress(bandwidth_data, throughput_data)
    r_squared_linear = r_value**2

    # Generate smooth line for visualization
    bandwidth_smooth = [min(bandwidth_data) + (max(bandwidth_data) - min(bandwidth_data)) * i / 99 for i in range(100)]
    throughput_pred = [intercept + slope * bw for bw in bandwidth_smooth]

    # Create visualization
    observed_linear = alt.Chart(df).mark_circle(size=100, color='steelblue').encode(
        x=alt.X('Memory Bandwidth (GB/s):Q', title='Memory Bandwidth (GB/s)'),
        y=alt.Y('Throughput (tiles/sec):Q'),
        tooltip=['Memory Bandwidth (GB/s)', 'Throughput (tiles/sec)']
    )

    pred_df_linear = pl.DataFrame({
        'Memory Bandwidth (GB/s)': bandwidth_smooth,
        'Predicted Throughput': throughput_pred
    })

    fitted_linear = alt.Chart(pred_df_linear).mark_line(color='orange', strokeWidth=2).encode(
        x='Memory Bandwidth (GB/s):Q',
        y=alt.Y('Predicted Throughput:Q', title='Throughput (tiles/sec)'),
        tooltip=['Memory Bandwidth (GB/s)', 'Predicted Throughput']
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
    - **Slope:** {slope:.3f} tiles/sec per GB/s
    - **Intercept:** {intercept:.3f} tiles/sec

    **Model Performance:**
    - **R² score:** {r_squared_linear:.4f}

    **Interpretation:**
    - For every **1 GB/s** increase in memory bandwidth, throughput increases by **{slope:.3f} tiles/sec**
    - The model explains **{r_squared_linear*100:.2f}%** of the variance in throughput
    """)

    mo.vstack([linear_chart, linear_summary])
    return


@app.cell
def _(alt, df, mo, pl):
    from scipy.optimize import curve_fit
    import numpy as np

    # Define logistic function: f(x) = L / (1 + exp(-k*(x-x0)))
    def _logistic_func(x, L, k, x0):
        return L / (1 + [1 / (1 + (-k * (xi - x0))**i / _factorial(i)) for i in range(1, 10) for xi in [x]][0] if isinstance(x, (int, float)) else [L / (1 + sum((-k * (xi - x0))**i / _factorial(i) for i in range(1, 10))) for xi in x])

    def _factorial(n):
        result = 1
        for i in range(2, n + 1):
            result *= i
        return result

    # Simpler approach using numpy
    threads_data = df['Threads'].to_numpy()
    bandwidth_values = df['Memory Bandwidth (GB/s)'].to_numpy()

    def _log_func(x, L, k, x0):
        return L / (1 + np.exp(-k * (x - x0)))

    # Fit logistic model
    popt, pcov = curve_fit(_log_func, threads_data, bandwidth_values, p0=[35, 1, 5])
    L_fit, k_fit, x0_fit = popt

    # Generate smooth curve
    threads_smooth = np.linspace(1, 12, 100)
    bandwidth_pred = _log_func(threads_smooth, L_fit, k_fit, x0_fit)

    # Calculate R²
    residuals = bandwidth_values - _log_func(threads_data, L_fit, k_fit, x0_fit)
    ss_res = np.sum(residuals**2)
    ss_tot = np.sum((bandwidth_values - np.mean(bandwidth_values))**2)
    r_squared = 1 - (ss_res / ss_tot)

    # Create visualization
    observed_logistic = alt.Chart(df).mark_circle(size=100, color='steelblue').encode(
        x=alt.X('Threads:Q', title='Number of Threads'),
        y=alt.Y('Memory Bandwidth (GB/s):Q'),
        tooltip=['Threads', 'Memory Bandwidth (GB/s)']
    )

    pred_df = pl.DataFrame({
        'Threads': threads_smooth.tolist(),
        'Predicted Bandwidth': bandwidth_pred.tolist()
    })

    fitted_logistic = alt.Chart(pred_df).mark_line(color='orange', strokeWidth=2).encode(
        x='Threads:Q',
        y=alt.Y('Predicted Bandwidth:Q', title='Memory Bandwidth (GB/s)'),
        tooltip=['Threads', 'Predicted Bandwidth']
    )

    logistic_chart = (observed_logistic + fitted_logistic).properties(
        width=600,
        height=400,
        title='Logistic Regression: Memory Bandwidth vs Thread Count'
    )

    # Create summary
    logistic_summary = mo.md(f"""
    ## Bandwidth vs Thread Count (Logistic)

    **Model Equation:**
    `Bandwidth = {L_fit:.3f} / (1 + exp(-{k_fit:.3f} × (Threads - {x0_fit:.3f})))`

    **Fitted Parameters:**
    - **L (Maximum capacity):** {L_fit:.3f} GB/s
    - **k (Growth rate):** {k_fit:.3f}
    - **x₀ (Midpoint):** {x0_fit:.3f} threads

    **Model Performance:**
    - **R² score:** {r_squared:.4f}

    **Interpretation:**
    - The model predicts a maximum bandwidth capacity of **{L_fit:.2f} GB/s**
    - The bandwidth reaches 50% of maximum at **{x0_fit:.2f} threads**
    - The model explains **{r_squared*100:.2f}%** of the variance in bandwidth
    """)

    mo.vstack([logistic_chart, logistic_summary])
    return L_fit, curve_fit, k_fit, np, threads_smooth, x0_fit


@app.cell
def _(L_fit, alt, df, k_fit, mo, np, pl, threads_smooth, x0_fit):
    # Calculate the derivative of the logistic function
    # d/dx[L/(1+exp(-k*(x-x0)))] = L*k*exp(-k*(x-x0))/(1+exp(-k*(x-x0)))^2
    def _logistic_derivative(x, L, k, x0):
        exp_term = np.exp(-k * (x - x0))
        return L * k * exp_term / (1 + exp_term)**2

    # Calculate analytical derivative values
    bandwidth_derivative = _logistic_derivative(threads_smooth, L_fit, k_fit, x0_fit)

    # Calculate approximate derivative from observed data using finite differences
    threads_obs = df['Threads'].to_numpy()
    bandwidth_obs = df['Memory Bandwidth (GB/s)'].to_numpy()

    # Use central differences where possible, forward/backward at endpoints
    approx_derivative = np.zeros(len(threads_obs))
    for i in range(len(threads_obs)):
        if i == 0:
            # Forward difference
            approx_derivative[i] = (bandwidth_obs[i+1] - bandwidth_obs[i]) / (threads_obs[i+1] - threads_obs[i])
        elif i == len(threads_obs) - 1:
            # Backward difference
            approx_derivative[i] = (bandwidth_obs[i] - bandwidth_obs[i-1]) / (threads_obs[i] - threads_obs[i-1])
        else:
            # Central difference
            approx_derivative[i] = (bandwidth_obs[i+1] - bandwidth_obs[i-1]) / (threads_obs[i+1] - threads_obs[i-1])

    # Find the maximum rate of change
    max_derivative_idx = np.argmax(bandwidth_derivative)
    max_derivative_threads = threads_smooth[max_derivative_idx]
    max_derivative_value = bandwidth_derivative[max_derivative_idx]

    # Create analytical derivative visualization
    derivative_df = pl.DataFrame({
        'Threads': threads_smooth.tolist(),
        'Rate of Change': bandwidth_derivative.tolist()
    })

    analytical_line = alt.Chart(derivative_df).mark_line(color='green', strokeWidth=2).encode(
        x=alt.X('Threads:Q', title='Number of Threads'),
        y=alt.Y('Rate of Change:Q', title='dBandwidth/dThreads (GB/s per thread)'),
        tooltip=['Threads', 'Rate of Change']
    )

    # Create approximate derivative visualization
    approx_df = pl.DataFrame({
        'Threads': threads_obs.tolist(),
        'Approximate Rate': approx_derivative.tolist()
    })

    approximate_points = alt.Chart(approx_df).mark_circle(size=100, color='purple', opacity=0.7).encode(
        x='Threads:Q',
        y=alt.Y('Approximate Rate:Q', title='dBandwidth/dThreads (GB/s per thread)'),
        tooltip=['Threads', 'Approximate Rate']
    )

    approximate_line = alt.Chart(approx_df).mark_line(color='purple', strokeWidth=1, strokeDash=[5,5], opacity=0.7).encode(
        x='Threads:Q',
        y='Approximate Rate:Q'
    )

    # Add point at maximum analytical derivative
    max_point = alt.Chart(pl.DataFrame({
        'Threads': [max_derivative_threads],
        'Rate of Change': [max_derivative_value]
    })).mark_point(size=200, color='red', filled=True).encode(
        x='Threads:Q',
        y='Rate of Change:Q',
        tooltip=[
            alt.Tooltip('Threads:Q', format='.2f'),
            alt.Tooltip('Rate of Change:Q', format='.3f')
        ]
    )

    derivative_viz = (analytical_line + approximate_line + approximate_points + max_point).properties(
        width=600,
        height=400,
        title='Derivative Analysis: Analytical vs Approximate'
    )

    # Create summary
    derivative_summary = mo.md(f"""
    ## Derivative Analysis: Rate of Bandwidth Growth

    **Peak Efficiency (Analytical):**
    - **Maximum rate of change:** {max_derivative_value:.3f} GB/s per thread
    - **Occurs at:** {max_derivative_threads:.2f} threads

    **Comparison:**
    - **Green line:** Analytical derivative from logistic model
    - **Purple points/line:** Approximate derivative from observed data (finite differences)
    - **Red dot:** Peak of analytical derivative (inflection point)

    **Interpretation:**
    - The derivative shows how quickly bandwidth increases as we add more threads
    - The peak occurs at **{max_derivative_threads:.2f} threads**, which is the inflection point (x₀={x0_fit:.3f})
    - The approximate derivative (purple) closely follows the analytical curve, validating the logistic model
    - Before the peak, adding threads provides increasing returns
    - After the peak, returns diminish as we approach the bandwidth limit of {L_fit:.2f} GB/s
    - The derivative approaches zero as thread count increases, indicating bandwidth saturation
    """)

    mo.vstack([derivative_viz, derivative_summary])
    return


@app.cell
def _(mo):
    # Create slider to select number of data points
    n_points_slider = mo.ui.slider(3, 12, value=12, label="Number of data points to fit", step=1)
    n_points_slider
    return (n_points_slider,)


@app.cell
def _(L_fit, alt, curve_fit, df, k_fit, mo, n_points_slider, np, pl, x0_fit):
    # Subset data and fit logistic model
    subset_df = df.head(n_points_slider.value)

    threads_subset = subset_df['Threads'].to_numpy()
    bandwidth_subset = subset_df['Memory Bandwidth (GB/s)'].to_numpy()

    # Define logistic function (needed in this cell)
    def _log_func_local(x, L, k, x0):
        return L / (1 + np.exp(-k * (x - x0)))

    # Fit logistic model to subset
    try:
        popt_subset, _ = curve_fit(_log_func_local, threads_subset, bandwidth_subset, p0=[35, 1, 5], maxfev=10000)
        L_sub, k_sub, x0_sub = popt_subset
    
        # Generate predictions
        threads_full = np.linspace(1, 12, 100)
        bandwidth_pred_subset = _log_func_local(threads_full, L_sub, k_sub, x0_sub)
    
        # Calculate R² on subset
        residuals_sub = bandwidth_subset - _log_func_local(threads_subset, L_sub, k_sub, x0_sub)
        ss_res_sub = np.sum(residuals_sub**2)
        ss_tot_sub = np.sum((bandwidth_subset - np.mean(bandwidth_subset))**2)
        r_squared_sub = 1 - (ss_res_sub / ss_tot_sub)
    
        # Create visualization with full data for context
        full_data = alt.Chart(df).mark_circle(size=80, color='lightgray', opacity=0.5).encode(
            x=alt.X('Threads:Q', title='Number of Threads'),
            y=alt.Y('Memory Bandwidth (GB/s):Q', scale=alt.Scale(domain=[0, 35])),
            tooltip=['Threads', 'Memory Bandwidth (GB/s)']
        )
    
        subset_data = alt.Chart(subset_df).mark_circle(size=100, color='steelblue').encode(
            x='Threads:Q',
            y='Memory Bandwidth (GB/s):Q',
            tooltip=['Threads', 'Memory Bandwidth (GB/s)']
        )
    
        pred_df_subset = pl.DataFrame({
            'Threads': threads_full.tolist(),
            'Predicted Bandwidth': bandwidth_pred_subset.tolist()
        })
    
        fitted_curve = alt.Chart(pred_df_subset).mark_line(color='orange', strokeWidth=3).encode(
            x='Threads:Q',
            y=alt.Y('Predicted Bandwidth:Q', title='Memory Bandwidth (GB/s)'),
            tooltip=['Threads', 'Predicted Bandwidth']
        )
    
        # Original full-data fit for comparison
        bandwidth_pred_full = _log_func_local(threads_full, L_fit, k_fit, x0_fit)
        original_fit = alt.Chart(pl.DataFrame({
            'Threads': threads_full.tolist(),
            'Original Fit': bandwidth_pred_full.tolist()
        })).mark_line(color='green', strokeWidth=2, strokeDash=[5,5], opacity=0.7).encode(
            x='Threads:Q',
            y=alt.Y('Original Fit:Q', title='Memory Bandwidth (GB/s)'),
            tooltip=['Threads', 'Original Fit']
        )
    
        subset_chart = (full_data + subset_data + fitted_curve + original_fit).properties(
            width=700,
            height=400,
            title=f'Logistic Fit Using First {n_points_slider.value} Data Points'
        )
    
        summary = mo.md(f"""
        ## Logistic Fit with {n_points_slider.value} Data Points
    
        **Model Parameters:**
        - **L (Max capacity):** {L_sub:.3f} GB/s (original: {L_fit:.3f})
        - **k (Growth rate):** {k_sub:.3f} (original: {k_fit:.3f})
        - **x₀ (Midpoint):** {x0_sub:.3f} threads (original: {x0_fit:.3f})
    
        **Model Performance:**
        - **R² score:** {r_squared_sub:.4f}
    
        **Visualization:**
        - **Gray circles:** All 12 data points (for reference)
        - **Blue circles:** Data points used for fitting
        - **Orange line:** Fit from subset of data
        - **Dashed green line:** Original fit from all 12 points
    
        **Observations:**
        - As more data points are added, the fit becomes more stable
        - Early points may underestimate the maximum capacity (L)
        - The growth rate (k) and midpoint (x₀) converge as we add more data
        """)
    
        result = mo.vstack([n_points_slider, subset_chart, summary])
    
    except Exception as e:
        error_message = mo.md(f"""
        ### Unable to fit model with {n_points_slider.value} points
    
        Error: {str(e)}
    
        **Note:** Logistic regression requires at least 3 data points and may fail with very few points due to insufficient information to estimate all three parameters (L, k, x₀).
        """)
        result = mo.vstack([n_points_slider, error_message])

    result
    return


if __name__ == "__main__":
    app.run()
