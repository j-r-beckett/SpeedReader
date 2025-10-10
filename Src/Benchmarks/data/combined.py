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
    def _():
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

        return mo.vstack([
            mo.md(f"""
            ## Throughput Analysis by Noise Level

            **Selected range:** Noise levels {noise_range.value[0]} to {noise_range.value[1]}
            """),
            noise_range,
            throughput_chart,
            memory_bw_chart
        ])

    _()
    return


@app.cell
def _(mo):
    mo.md(
        r"""
    `dbnet_bandwidth = max_bandwidth / (1 + exp(k * (threads - x_0)))`

    For no noise DBNet:
    - `max_bandwidth = 32.392`
    - `k = 0.826`
    - `x_0 = 2.295`

    `svtr_bandwidth = intercept + slope * threads`

    For no noise SVTR:
    - `slope = 0.556` GB/s per thread
    - `intercept = 1.304` GB/s

    The data we're working with was generated with DBNet as the primary source and SVTR as the noise. So:

    `bandwidth = dbnet_bandwidth + svtr_bandwidth = (max_bandwidth - intercept + slope * noise) / (1 + exp(k * (threads - x_0))) + intercept + slope * noise`
    """
    )
    return


@app.cell
def _(MAX_NOISE_LEVEL, mo):
    # Create a range slider to select which noise levels to display
    noise_viz_range = mo.ui.range_slider(
        start=0,
        stop=MAX_NOISE_LEVEL,
        value=[0, MAX_NOISE_LEVEL],
        step=1,
        label="Noise Level Range",
        full_width=True
    )
    noise_viz_range
    return (noise_viz_range,)


@app.cell
def _(MAX_NOISE_LEVEL, alt, df, mo, noise_viz_range, pl):
    def _():
        import numpy as np
    
        # Model parameters
        max_bandwidth = 32.392
        k = 0.826
        x_0 = 2.295
        svtr_slope = 0.556
        svtr_intercept = 1.304
    
        # Generate theoretical curves for selected noise levels
        threads_range = np.linspace(1, 8, 100)
    
        theoretical_rows = []
        selected_noise_levels = list(range(noise_viz_range.value[0], noise_viz_range.value[1] + 1))
    
        for noise_level in selected_noise_levels:
            for threads in threads_range:
                # SVTR bandwidth (linear with noise)
                svtr_bw = svtr_intercept + svtr_slope * noise_level
            
                # DBNet bandwidth (logistic function with reduced asymptote)
                dbnet_bw = (max_bandwidth - svtr_bw) / (1 + np.exp(-k * (threads - x_0)))
            
                # Total bandwidth
                total_bw = dbnet_bw + svtr_bw
            
                theoretical_rows.append({
                    'noise': noise_level,
                    'threads': threads,
                    'memory_bw': total_bw
                })
    
        theoretical_df = pl.DataFrame(theoretical_rows)
    
        # Filter actual data for selected noise levels
        filtered_df = df.filter(
            (pl.col('noise') >= noise_viz_range.value[0]) &
            (pl.col('noise') <= noise_viz_range.value[1])
        )
    
        # Create line chart for theoretical curves with fixed color domain
        theoretical_chart = alt.Chart(theoretical_df).mark_line().encode(
            x=alt.X('threads:Q', title='Threads', scale=alt.Scale(domain=[1, 8])),
            y=alt.Y('memory_bw:Q', title='Memory Bandwidth (GB/s)'),
            color=alt.Color('noise:N', title='Noise Level', 
                           scale=alt.Scale(scheme='tableau10', domain=list(range(MAX_NOISE_LEVEL + 1))))
        )
    
        # Create scatter plot for actual data points with same fixed color domain
        actual_chart = alt.Chart(filtered_df).mark_circle(size=80, opacity=0.7).encode(
            x=alt.X('threads:Q'),
            y=alt.Y('memory_bw:Q'),
            color=alt.Color('noise:N', title='Noise Level', 
                           scale=alt.Scale(scheme='tableau10', domain=list(range(MAX_NOISE_LEVEL + 1)))),
            tooltip=[
                alt.Tooltip('noise:N', title='Noise'),
                alt.Tooltip('threads:Q', title='Threads'),
                alt.Tooltip('memory_bw:Q', title='Memory BW', format='.2f')
            ]
        )
    
        # Combine charts
        combined_chart = (theoretical_chart + actual_chart).properties(
            width=800,
            height=500,
            title='Theoretical Model vs Actual Memory Bandwidth'
        )
    
        return mo.vstack([
            mo.md(f"""
            ## Bandwidth Model Visualization
        
            **Selected range:** Noise levels {noise_viz_range.value[0]} to {noise_viz_range.value[1]}
        
            **Model:** `bandwidth = (max_bw - svtr_bw) / (1 + exp(-k × (threads - x₀))) + svtr_bw`
        
            Where:
            - `max_bw = {max_bandwidth}` GB/s
            - `k = {k}`
            - `x₀ = {x_0}`
            - `svtr_bw = {svtr_intercept} + {svtr_slope} × noise`
        
            Lines show theoretical curves, points show actual measurements.
            """),
            noise_viz_range,
            combined_chart
        ])

    _()
    return


@app.cell
def _(df, mo, pl):
    from sklearn.metrics import r2_score
    import numpy as np

    # Model parameters
    max_bandwidth = 32.392
    k = 0.826
    x_0 = 2.295
    svtr_slope = 0.556
    svtr_intercept = 1.304

    # Calculate R² for each noise level
    r2_results = []

    for noise_level in sorted(df['noise'].unique()):
        # Filter actual data for this noise level
        noise_df = df.filter(pl.col('noise') == noise_level)
    
        # Get actual values
        threads = noise_df['threads'].to_numpy()
        actual_bw = noise_df['memory_bw'].to_numpy()
    
        # Calculate theoretical values
        svtr_bw = svtr_intercept + svtr_slope * noise_level
        dbnet_bw = (max_bandwidth - svtr_bw) / (1 + np.exp(-k * (threads - x_0)))
        theoretical_bw = dbnet_bw + svtr_bw
    
        # Calculate R²
        r2 = r2_score(actual_bw, theoretical_bw)
    
        r2_results.append({
            'noise_level': noise_level,
            'r2_score': round(r2, 4),
            'num_points': len(actual_bw)
        })

    # Create DataFrame
    r2_df = pl.DataFrame(r2_results)

    # Calculate average R²
    avg_r2 = r2_df['r2_score'].mean()

    mo.vstack([
        mo.md("""
        ## R² Scores: Theoretical Model vs Actual Memory Bandwidth
    
        Comparing the theoretical model prediction against actual measurements for each noise level.
        """),
        r2_df,
        mo.md(f"""
        ### Summary
    
        **Average R² Score:** {avg_r2:.4f}
    
        The theoretical model explains an average of {avg_r2*100:.2f}% of the variance in memory bandwidth across all noise levels.
        """)
    ])
    return


@app.cell
def _(df, mo, pl):
    def _():
        import numpy as np
        from sklearn.metrics import r2_score

        # Model parameters
        max_bandwidth = 32.392
        k = 0.826
        x_0 = 2.295
        svtr_slope = 0.556
        svtr_intercept = 1.304

        # Analyze the model's behavior
        results = []

        for noise_level in sorted(df['noise'].unique()):
            noise_df = df.filter(pl.col('noise') == noise_level)
        
            threads = noise_df['threads'].to_numpy()
            actual_bw = noise_df['memory_bw'].to_numpy()
        
            # Current model (as implemented)
            svtr_bw = svtr_intercept + svtr_slope * noise_level
            dbnet_bw = (max_bandwidth - svtr_bw) / (1 + np.exp(-k * (threads - x_0)))
            predicted_bw = dbnet_bw + svtr_bw
        
            # Calculate R² and residuals
            r2 = r2_score(actual_bw, predicted_bw)
            residuals = actual_bw - predicted_bw
            mean_residual = residuals.mean()
            max_residual = residuals.max()
            min_residual = residuals.min()
        
            # Check if DBNet effective asymptote is changing
            effective_dbnet_max = max_bandwidth - svtr_bw
        
            results.append({
                'noise': noise_level,
                'r2': round(r2, 4),
                'mean_residual': round(mean_residual, 3),
                'max_residual': round(max_residual, 3),
                'min_residual': round(min_residual, 3),
                'svtr_bw': round(svtr_bw, 3),
                'effective_dbnet_max': round(effective_dbnet_max, 3),
                'actual_max': round(actual_bw.max(), 3),
                'predicted_max': round(predicted_bw.max(), 3)
            })

        results_df = pl.DataFrame(results)

        return mo.vstack([
            mo.md("""
            ## Residual Analysis
        
            The model: `total_bw = (max_bw - svtr_bw) / (1 + exp(-k × (threads - x₀))) + svtr_bw`
        
            This assumes:
            1. SVTR takes a fixed bandwidth (independent of DBNet threads)
            2. DBNet asymptote reduces by exactly svtr_bw
            3. DBNet growth parameters (k, x₀) remain constant
        
            Let's check if assumptions 2 or 3 are violated:
            """),
            results_df,
            mo.md("""
            **Key observations:**
            - Is `mean_residual` systematic (always positive or negative)?
            - Does `actual_max` exceed `predicted_max` (underpredicting) or vice versa?
            - How does the gap between `effective_dbnet_max` and actual DBNet performance change?
            """)
        ])

    _()
    return


@app.cell
def _(df, mo, pl):
    def _():
        from sklearn.linear_model import LinearRegression
        import numpy as np

        # Prepare data for multiple regression
        # X will have two features: memory_bw and noise
        X = df.select(['memory_bw', 'noise']).to_numpy()
        y = df['throughput'].to_numpy()

        # Fit the model
        regression_model = LinearRegression()
        regression_model.fit(X, y)

        # Calculate R² score
        r2_score = regression_model.score(X, y)

        # Get coefficients
        bw_coef = regression_model.coef_[0]
        noise_coef = regression_model.coef_[1]
        intercept = regression_model.intercept_

        # Make predictions
        y_pred = regression_model.predict(X)

        # Add predictions to dataframe for visualization
        df_with_pred = df.with_columns([
            pl.lit(y_pred).alias('predicted_throughput'),
            pl.lit(y - y_pred).alias('residual')
        ])

        summary = mo.md(f"""
        ## Multiple Linear Regression Results

        **Model:** `throughput = {intercept:.3f} + {bw_coef:.3f} × memory_bw + {noise_coef:.3f} × noise`

        - **R² Score:** {r2_score:.4f}
        - **Intercept:** {intercept:.3f}
        - **Memory Bandwidth Coefficient:** {bw_coef:.3f}
        - **Noise Level Coefficient:** {noise_coef:.3f}

        The model explains {r2_score*100:.2f}% of the variance in throughput.
        """)

        return summary, df_with_pred

    regression_summary, df_with_pred = _()
    regression_summary
    return (df_with_pred,)


@app.cell
def _(alt, df_with_pred, pl):
    def _():
        # Visualize actual vs predicted throughput
        scatter = alt.Chart(df_with_pred).mark_circle(size=60, opacity=0.6).encode(
            x=alt.X('predicted_throughput:Q', title='Predicted Throughput (inferences/sec)'),
            y=alt.Y('throughput:Q', title='Actual Throughput (inferences/sec)'),
            color=alt.Color('noise:N', title='Noise Level', scale=alt.Scale(scheme='tableau10')),
            tooltip=[
                alt.Tooltip('noise:N', title='Noise'),
                alt.Tooltip('threads:Q', title='Threads'),
                alt.Tooltip('throughput:Q', title='Actual Throughput', format='.2f'),
                alt.Tooltip('predicted_throughput:Q', title='Predicted', format='.2f'),
                alt.Tooltip('residual:Q', title='Residual', format='.2f'),
                alt.Tooltip('memory_bw:Q', title='Memory BW', format='.2f')
            ]
        ).properties(
            width=500,
            height=500,
            title='Actual vs Predicted Throughput'
        )

        # Add diagonal line for perfect prediction
        line_data = pl.DataFrame({
            'x': [df_with_pred['throughput'].min(), df_with_pred['throughput'].max()],
            'y': [df_with_pred['throughput'].min(), df_with_pred['throughput'].max()]
        })

        diagonal = alt.Chart(line_data).mark_line(color='red', strokeDash=[5, 5]).encode(
            x='x:Q',
            y='y:Q'
        )

        return scatter + diagonal

    _()
    return


@app.cell
def _(alt, df_with_pred, pl):
    def _():
        # Visualize residuals
        residual_chart = alt.Chart(df_with_pred).mark_circle(size=60, opacity=0.6).encode(
            x=alt.X('predicted_throughput:Q', title='Predicted Throughput (inferences/sec)'),
            y=alt.Y('residual:Q', title='Residual (Actual - Predicted)', scale=alt.Scale(zero=False)),
            color=alt.Color('noise:N', title='Noise Level', scale=alt.Scale(scheme='tableau10')),
            tooltip=[
                alt.Tooltip('noise:N', title='Noise'),
                alt.Tooltip('threads:Q', title='Threads'),
                alt.Tooltip('residual:Q', title='Residual', format='.2f'),
                alt.Tooltip('memory_bw:Q', title='Memory BW', format='.2f')
            ]
        ).properties(
            width=600,
            height=400,
            title='Residual Plot'
        )

        # Add horizontal line at y=0
        zero_line = alt.Chart(pl.DataFrame({'y': [0]})).mark_rule(color='red', strokeDash=[5, 5]).encode(y='y:Q')

        return residual_chart + zero_line

    _()
    return


@app.cell
def _(df, mo, pl):
    def _():
        from scipy.optimize import curve_fit
        from sklearn.metrics import r2_score
        import numpy as np

        # Define logistic function: y = L / (1 + exp(-k*(x - x0)))
        def logistic(x, L, k, x0):
            return L / (1 + np.exp(-k * (x - x0)))

        # Fit a logistic function for each noise level
        results = []

        for noise_level in sorted(df['noise'].unique()):
            # Filter data for this noise level
            noise_df = df.filter(pl.col('noise') == noise_level)
        
            # Use threads as x and throughput as y
            x = noise_df['threads'].to_numpy()
            y = noise_df['throughput'].to_numpy()
        
            # Initial parameter guesses
            L_init = y.max() * 1.2  # asymptote slightly above max
            k_init = 1.0  # steepness
            x0_init = x.mean()  # midpoint
        
            try:
                # Fit the logistic curve
                params, _ = curve_fit(logistic, x, y, p0=[L_init, k_init, x0_init], maxfev=10000)
                L, k, x0 = params
            
                # Calculate R² score
                y_pred = logistic(x, L, k, x0)
                r2 = r2_score(y, y_pred)
            
                results.append({
                    'noise': noise_level,
                    'L': round(L, 3),
                    'k': round(k, 3),
                    'x0': round(x0, 3),
                    'r2': round(r2, 4)
                })
            except Exception as e:
                print(f"Failed to fit noise level {noise_level}: {e}")

        # Create results DataFrame with description
        results_df = pl.DataFrame(results)
    
        return mo.vstack([
            mo.md("""
            ## Logistic Regression Results
        
            **Model:** `throughput = L / (1 + exp(-k × (threads - x₀)))`
        
            Where:
            - **L**: Maximum throughput (asymptote)
            - **k**: Steepness of the curve
            - **x₀**: Midpoint (threads at 50% of maximum throughput)
            - **R²**: Coefficient of determination
            """),
            results_df
        ])

    _()
    return


@app.cell
def _(MAX_NOISE_LEVEL, mo):
    # Create a range slider to select which noise levels to display
    logistic_noise_range = mo.ui.range_slider(
        start=0,
        stop=MAX_NOISE_LEVEL,
        value=[0, MAX_NOISE_LEVEL],
        step=1,
        label="Noise Level Range",
        full_width=True
    )
    logistic_noise_range
    return (logistic_noise_range,)


@app.cell
def _(MAX_NOISE_LEVEL, alt, df, logistic_noise_range, mo, pl):
    def _():
        from scipy.optimize import curve_fit
        import numpy as np

        # Define logistic function
        def logistic(x, L, k, x0):
            return L / (1 + np.exp(-k * (x - x0)))

        # Fit logistic functions and prepare data for visualization
        theoretical_rows = []
        selected_noise_levels = list(range(logistic_noise_range.value[0], logistic_noise_range.value[1] + 1))

        for noise_level in selected_noise_levels:
            noise_df = df.filter(pl.col('noise') == noise_level)
        
            x = noise_df['threads'].to_numpy()
            y = noise_df['memory_bw'].to_numpy()
        
            # Fit the logistic curve
            L_init = y.max() * 1.2
            k_init = 1.0
            x0_init = x.mean()
        
            try:
                params, _ = curve_fit(logistic, x, y, p0=[L_init, k_init, x0_init], maxfev=10000)
                L, k, x0 = params
            
                # Generate theoretical curve
                threads_range = np.linspace(1, 8, 100)
                for threads in threads_range:
                    theoretical_rows.append({
                        'noise': noise_level,
                        'threads': threads,
                        'memory_bw': logistic(threads, L, k, x0)
                    })
            except Exception as e:
                print(f"Failed to fit noise level {noise_level}: {e}")

        theoretical_df = pl.DataFrame(theoretical_rows)
    
        # Filter actual data for selected noise levels
        filtered_df = df.filter(
            (pl.col('noise') >= logistic_noise_range.value[0]) &
            (pl.col('noise') <= logistic_noise_range.value[1])
        )
    
        # Create line chart for logistic curves
        theoretical_chart = alt.Chart(theoretical_df).mark_line().encode(
            x=alt.X('threads:Q', title='Threads', scale=alt.Scale(domain=[1, 8])),
            y=alt.Y('memory_bw:Q', title='Memory Bandwidth (GB/s)'),
            color=alt.Color('noise:N', title='Noise Level', 
                           scale=alt.Scale(scheme='tableau10', domain=list(range(MAX_NOISE_LEVEL + 1))))
        )
    
        # Create scatter plot for actual data points
        actual_chart = alt.Chart(filtered_df).mark_circle(size=80, opacity=0.7).encode(
            x=alt.X('threads:Q'),
            y=alt.Y('memory_bw:Q'),
            color=alt.Color('noise:N', title='Noise Level', 
                           scale=alt.Scale(scheme='tableau10', domain=list(range(MAX_NOISE_LEVEL + 1)))),
            tooltip=[
                alt.Tooltip('noise:N', title='Noise'),
                alt.Tooltip('threads:Q', title='Threads'),
                alt.Tooltip('memory_bw:Q', title='Memory BW', format='.2f')
            ]
        )
    
        # Combine charts
        combined_chart = (theoretical_chart + actual_chart).properties(
            width=800,
            height=500,
            title='Logistic Regression: Memory Bandwidth vs Thread Count by Noise Level'
        )
    
        return mo.vstack([
            mo.md(f"""
            ## Logistic Regression Visualization
        
            **Selected range:** Noise levels {logistic_noise_range.value[0]} to {logistic_noise_range.value[1]}
        
            **Model:** `memory_bw = L / (1 + exp(-k × (threads - x₀)))`
        
            Lines show fitted logistic curves, points show actual measurements.
            """),
            logistic_noise_range,
            combined_chart
        ])

    _()
    return


@app.cell
def _(alt, df, mo, pl):
    def _():
        from scipy.optimize import curve_fit
        from sklearn.metrics import r2_score
        import numpy as np

        # Define logistic function
        def logistic(x, L, k, x0):
            return L / (1 + np.exp(-k * (x - x0)))

        # Fit logistic functions for each noise level
        results = []

        for noise_level in sorted(df['noise'].unique()):
            noise_df = df.filter(pl.col('noise') == noise_level)
        
            x = noise_df['threads'].to_numpy()
            y = noise_df['memory_bw'].to_numpy()
        
            L_init = y.max() * 1.2
            k_init = 1.0
            x0_init = x.mean()
        
            try:
                params, _ = curve_fit(logistic, x, y, p0=[L_init, k_init, x0_init], maxfev=10000)
                L, k, x0 = params
            
                y_pred = logistic(x, L, k, x0)
                r2 = r2_score(y, y_pred)
            
                results.append({
                    'noise': noise_level,
                    'L': L,
                    'k': k,
                    'x0': x0,
                    'r2': r2
                })
            except Exception as e:
                print(f"Failed to fit noise level {noise_level}: {e}")

        results_df = pl.DataFrame(results)
    
        # Create visualizations for L, k, and x0
        L_chart = alt.Chart(results_df).mark_line(point=True, color='steelblue').encode(
            x=alt.X('noise:Q', title='Noise Level'),
            y=alt.Y('L:Q', title='L (Asymptote)', scale=alt.Scale(zero=True)),
            tooltip=[
                alt.Tooltip('noise:Q', title='Noise'),
                alt.Tooltip('L:Q', title='L', format='.3f'),
                alt.Tooltip('r2:Q', title='R²', format='.4f')
            ]
        ).properties(
            width=300,
            height=250,
            title='Maximum Bandwidth (L) vs Noise Level'
        )
    
        k_chart = alt.Chart(results_df).mark_line(point=True, color='coral').encode(
            x=alt.X('noise:Q', title='Noise Level'),
            y=alt.Y('k:Q', title='k (Steepness)', scale=alt.Scale(zero=True)),
            tooltip=[
                alt.Tooltip('noise:Q', title='Noise'),
                alt.Tooltip('k:Q', title='k', format='.3f'),
                alt.Tooltip('r2:Q', title='R²', format='.4f')
            ]
        ).properties(
            width=300,
            height=250,
            title='Curve Steepness (k) vs Noise Level'
        )
    
        x0_chart = alt.Chart(results_df).mark_line(point=True, color='seagreen').encode(
            x=alt.X('noise:Q', title='Noise Level'),
            y=alt.Y('x0:Q', title='x₀ (Midpoint)', scale=alt.Scale(zero=True)),
            tooltip=[
                alt.Tooltip('noise:Q', title='Noise'),
                alt.Tooltip('x0:Q', title='x₀', format='.3f'),
                alt.Tooltip('r2:Q', title='R²', format='.4f')
            ]
        ).properties(
            width=300,
            height=250,
            title='Midpoint (x₀) vs Noise Level'
        )
    
        r2_chart = alt.Chart(results_df).mark_line(point=True, color='purple').encode(
            x=alt.X('noise:Q', title='Noise Level'),
            y=alt.Y('r2:Q', title='R² Score', scale=alt.Scale(domain=[0, 1])),
            tooltip=[
                alt.Tooltip('noise:Q', title='Noise'),
                alt.Tooltip('r2:Q', title='R²', format='.4f')
            ]
        ).properties(
            width=300,
            height=250,
            title='Model Fit Quality (R²) vs Noise Level'
        )
    
        return mo.vstack([
            mo.md("""
            ## Logistic Regression Parameters by Noise Level
        
            **Model:** `memory_bw = L / (1 + exp(-k × (threads - x₀)))`
        
            Analyzing how the logistic curve parameters change as noise increases:
            """),
            results_df,
            mo.hstack([L_chart, k_chart]),
            mo.hstack([x0_chart, r2_chart])
        ])

    _()
    return


if __name__ == "__main__":
    app.run()
