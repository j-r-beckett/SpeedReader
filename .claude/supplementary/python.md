# Python Script Pattern

All Python scripts in this repo follow a standard pattern. Match it exactly.

## Skeleton

```python
#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "utils"]
#
# [tool.uv.sources]
# utils = { path = "RELATIVE_PATH/tools/utils", editable = true }
# ///

"""
One-line description.

Usage:
    ./script.py [options]
"""

from pathlib import Path
import click
from utils import bash, info, error, ScriptError

SCRIPT_DIR = Path(__file__).parent.resolve()


@click.command()
def main():
    info("doing something")


if __name__ == "__main__":
    try:
        main()
    except ScriptError as e:
        error(f"Fatal: {e}")
        exit(1)
```

## Key Elements

- **Shebang**: `#!/usr/bin/env -S uv run --script` - makes script directly executable
- **PEP 723 metadata**: Inline dependencies, always include `utils`
- **utils path**: Adjust `RELATIVE_PATH` based on script location (e.g., `../tools/utils` from `ci/`)
- **SCRIPT_DIR**: Anchors all path operations
- **Click**: Use for CLI arguments/options
- **Error handling**: Wrap main() in try/except for ScriptError

## Python Over bash()

**Prefer Python-native solutions over bash() for portability.** These scripts may be ported to other platforms.

| Instead of | Use |
|------------|-----|
| `bash("echo ...")` | `print()` or `info()` |
| `bash("mkdir -p ...")` | `Path.mkdir(parents=True, exist_ok=True)` |
| `bash("rm -rf ...")` | `shutil.rmtree()` |
| `bash("cp ...")` | `shutil.copy2()` |
| `bash("cat file")` | `Path.read_text()` |
| `bash("ls ...")` | `Path.glob()` or `Path.iterdir()` |
| `bash` with `grep/awk/sed` | `re` module |

**Use bash() only for:**
- Invoking external tools (git, docker, compilers, zig, etc.)
- Commands with no Python equivalent

## Available from utils

- `bash(cmd, directory=)` - Run shell command with streaming output
- `info(msg)` - Green status message
- `error(msg)` - Red error message
- `ensure_repo(path, url, tag)` - Clone/checkout git repo at version
- `format_duration(seconds)` - Human-readable time
- `ScriptError` - Raise for fatal errors

## Reference

See existing scripts for real examples:
- `ci/act.py` - CLI tool with multiple options
- `src/Native/onnx/build.py` - Build script with version checking
- `models/build_svtr.py` - Complex build with venv creation
