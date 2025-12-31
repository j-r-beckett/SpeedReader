---
name: marimo
description: Work effectively with marimo notebooks. Use before editing an existing notebook, and before creating a new notebook. (project)
---

# Marimo Notebooks

**Stack**: Pandas + Seaborn. Use these for data manipulation and visualization.

## Creating a Notebook

```bash
uvx marimo edit --sandbox notebook.py --mcp --no-token --watch
```

The `--sandbox` flag stores dependencies in the notebook file via inline metadata. Always include `--mcp --no-token --watch` to enable Claude integration.

**After creating or modifying a notebook, prompt the user with the full command:**
```
uvx marimo edit --sandbox notebooks/YOUR_NOTEBOOK.py --mcp --no-token --watch
```

## Notebook Structure

Marimo notebooks are Python files with `@app.cell` decorated functions:

```python
import marimo

app = marimo.App()


@app.cell
def imports(mo):
    import pandas as pd
    import seaborn as sns
    import matplotlib.pyplot as plt

    sns.set_theme()
    return pd, plt, sns


@app.cell
def load_data(pd):
    df = pd.read_csv("data.csv")
    return (df,)


@app.cell
def viz(df, plt, sns):
    fig, ax = plt.subplots(figsize=(10, 6))
    sns.histplot(data=df, x="value", ax=ax)
    ax  # Last expression displays automatically


if __name__ == "__main__":
    app.run()
```

## Gotchas

### Respect the Global Namespace

Marimo disallows redefining variables across cells. Wrap intermediate variables in `def _()` to keep them local:

```python
@app.cell
def analysis(df, pd):
    def _():
        grouped = df.groupby("category")
        means = grouped["value"].mean()
        stds = grouped["value"].std()
        return pd.DataFrame({"mean": means, "std": stds})

    summary = _()
    return (summary,)
```

### Interactive UI Elements: The Two-Cell Rule

UI element definition and `.value` usage MUST be in separate cells. When you interact with a UI element, marimo runs cells that **reference** the variable but **don't define** it—the defining cell never re-runs.

```python
# WRONG - same cell, never updates
@app.cell
def broken(mo):
    slider = mo.ui.slider(0, 100)
    result = slider.value * 2  # Never re-runs!
    return (slider,)

# CORRECT - two cells
@app.cell
def controls(mo):
    slider = mo.ui.slider(0, 100)
    return (slider,)

@app.cell
def compute(slider):
    result = slider.value * 2  # Re-runs on slider change
```

**Gotcha—UI reset**: If the defining cell re-runs (because a dependency changed), the element resets to its initial value. Keep UI-defining cells dependency-free.

### Don't Mutate Across Cells

Marimo doesn't track object mutations. Declare and mutate in the same cell, or create new objects:

```python
# WRONG - mutation in separate cell not tracked
@app.cell
def load(pd):
    df = pd.read_csv("data.csv")
    return (df,)

@app.cell
def transform(df):
    df["new_col"] = df["x"] * 2  # Not tracked!
    return (df,)

# CORRECT - create new object
@app.cell
def transform(df):
    df_transformed = df.assign(new_col=df["x"] * 2)
    return (df_transformed,)
```

## Validation

Always run after editing a notebook:

```bash
uvx marimo check --fix notebook.py
```

Note: Marimo auto-formats notebooks on save, which may:
- Rename cell functions (e.g., `def results(...)` → `def _(...)`)
- Adjust return statements and function parameters
- Add `__generated_with` version field

This is normal behavior. Don't fight the formatter.

## Tricks

### Caching Expensive Computations

- **`mo.cache`** - in-memory, fast, lost on kernel restart
- **`mo.persistent_cache`** - disk-based, slower, survives restarts

```python
@app.cell
def model(mo, df):
    @mo.persistent_cache
    def train_model(data):
        # Expensive - cached to disk, survives restarts
        return model.fit(data)

    trained = train_model(df)
    return (trained,)
```

Changing the cell code invalidates the cache automatically.

### Gate Expensive Operations with `mo.stop`

Prevent a cell from running until a condition is met:

```python
@app.cell
def expensive(mo, run_button, df):
    mo.stop(not run_button.value, mo.md("Click **Run** to execute"))
    # Code below only runs after button click
    result = expensive_computation(df)
    return (result,)
```

### Progress Bars

tqdm-like progress for long operations:

```python
# Iterate over a collection
@app.cell
def process(mo, data):
    results = []
    for item in mo.status.progress_bar(data, title="Processing"):
        results.append(transform(item))
    return (results,)
```

```python
# Known total with manual updates (e.g., streaming from subprocess)
@app.cell
def run_benchmark(mo):
    results = []
    with mo.status.progress_bar(total=1000, title="Running") as bar:
        for line in stream_output():
            results.append(parse(line))
            bar.update()
    return (results,)
```

## Using Local Packages

Reference local packages via `[tool.uv.sources]` in the notebook's inline metadata—the same pattern used by scripts in this project:

```python
# /// script
# requires-python = ">=3.11"
# dependencies = ["pandas", "seaborn", "build_utils"]
#
# [tool.uv.sources]
# build_utils = { path = "../build_utils", editable = true }
# ///
```

With `editable = true`, changes to the package reflect immediately without reinstalling.

## Sharing Code Between Notebooks

Use `@app.function` to create importable functions. These can only reference symbols from the setup block.

### CRITICAL: @app.function Corruption Bug

Marimo's editor **corrupts** `@app.function` on save, turning it into `app._unparsable_cell`. **NEVER edit a file containing `@app.function` through `marimo edit`.**

Recommended two-file pattern:

```python
# notebooks/helpers.py - Edit as RAW PYTHON only, never in marimo
# WARNING: Do not edit this file in marimo. Marimo corrupts @app.function on save.

import marimo

app = marimo.App()

with app.setup:
    import marimo as mo
    import pandas as pd

@app.function
def summary_table(df: pd.DataFrame):
    """Can use mo.* since this runs in marimo context."""
    return mo.ui.table(df.describe())

if __name__ == "__main__":
    app.run()
```

```python
# notebooks/analysis.py - Edit freely in marimo
import marimo

app = marimo.App()

with app.setup:
    import marimo as mo
    from helpers import summary_table  # Import in setup block

@app.cell
def show_table(df):
    summary_table(df)  # Use the imported function

if __name__ == "__main__":
    app.run()
```

This pattern allows:
- Claude to edit `helpers.py` as raw Python without corruption
- User to edit `analysis.py` freely in marimo
- Both files open simultaneously without conflict

For sharing across directories or with non-marimo code, extract to a proper Python module and use `[tool.uv.sources]` instead.

## Running Notebooks

Always prompt the user with the full command for their specific notebook:

```bash
uvx marimo edit --sandbox notebooks/benchmark.py --mcp --no-token --watch
```

Other commands:
```bash
# Run as script (no UI)
uv run notebook.py

# Export to HTML
uvx marimo export html notebook.py -o output.html
```

**IMPORTANT for MCP**: The notebook's inline dependencies must include `marimo[mcp]`, not just `marimo`:

```python
# /// script
# requires-python = ">=3.11"
# dependencies = ["marimo[mcp]", "pandas", "seaborn"]  # Note: marimo[mcp]
# ///
```

Without this, `--sandbox` mode will fail with "MCP dependencies not available".

The `--watch` flag auto-reloads when you edit the file, enabling a workflow where Claude edits the .py file and marimo reflects changes live.

**Watch mode limitation**: `--watch` only watches the notebook file itself, not imported modules. If Claude edits `helpers.py` while you have `analysis.py` open in marimo, you must restart marimo to pick up the changes. This is acceptable since `helpers.py` changes infrequently.

**If MCP tools fail with "fetch failed"**: Marimo isn't running. Start it with the command above.

## Headless Analysis with Playwright

To analyze notebook results without user interaction, export to HTML and use Playwright to extract data.

### Export to HTML

Use `--sandbox` to automatically use the notebook's inline dependencies:

```bash
uvx marimo export html --sandbox notebooks/my_notebook.py -o /tmp/output.html
```

### Extract Data with Playwright

1. Navigate to the exported HTML:
```
browser_navigate: url="file:///tmp/output.html"
```

2. Wait for JS to render, then snapshot:
```
browser_wait_for: time=1
browser_snapshot
```

3. The snapshot contains the full page accessibility tree, including data tables. Extract values from table cells in the YAML output.

### Limitations

- **Visualizations are impractical to capture** - charts require scrolling and screenshots, which is fragile
- **Data tables work well** - the accessibility tree includes all cell values
- Use this for automated analysis of tabular results, not visual verification
