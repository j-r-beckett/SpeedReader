import subprocess
from pathlib import Path
from rich.console import Console

console = Console()


class ScriptError(Exception):
    pass


def bash(command: str, directory: str | Path = None) -> str:
    """
    Execute a bash command in a specified directory, streaming output in real-time.

    Args:
        command: The bash command to execute
        directory: The directory in which to execute the command (defaults to script's directory)

    Returns:
        The combined stdout/stderr output as a string

    Raises:
        subprocess.CalledProcessError: If the command exits with a non-zero status code
    """
    # If directory is empty, use the script's directory
    if directory is None:
        directory = Path(__file__).parent

    # Convert directory to Path object if it's a string
    dir_path = Path(directory)

    # Print the command being executed in yellow
    console.print(f"$ {command}", style="yellow", highlight=False)

    # Execute the command using bash with streaming output
    process = subprocess.Popen(
        ["bash", "-c", command],
        cwd=dir_path,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,  # Merge stderr into stdout
        text=True,
        bufsize=1,  # Line buffered
    )

    # Capture output while streaming it in real-time
    output_lines = []
    for line in process.stdout:
        output_lines.append(line)
        console.print(line, style="bright_black", end="", highlight=False, markup=False)

    # Wait for process to complete and get return code
    returnCode = process.wait()

    # Raise exception if command failed
    if returnCode == 127:
        raise ScriptError(
            f"Command {command.split()[0]} not found. Are you missing a dependency?"
        )

    if returnCode != 0:
        raise ScriptError(f"Command {command} returned {returnCode}")

    # Return the captured output
    return "".join(output_lines)


def info(msg):
    console.print(msg, style="green", highlight=False, markup=False)


def error(msg):
    console.print(msg, style="red", highlight=False, markup=False)


def format_duration(seconds: float) -> str:
    """Format duration in human-readable form"""
    if seconds < 60:
        return f"{seconds:.1f}s"
    elif seconds < 3600:
        mins = int(seconds // 60)
        secs = int(seconds % 60)
        return f"{mins}m {secs}s"
    else:
        hours = int(seconds // 3600)
        mins = int((seconds % 3600) // 60)
        return f"{hours}h {mins}m"


def checkout_submodule(submodule_dir: Path, tag: str, name: str = None):
    """
    Checkout a specific tag of a git submodule, initializing it if necessary.

    Args:
        submodule_dir: Path to the submodule directory
        tag: Git tag to checkout
        name: Human-readable name for logging (defaults to directory name)
    """
    if name is None:
        name = submodule_dir.name

    # Initialize submodule if not already initialized (directory missing or empty)
    if not submodule_dir.exists() or not any(submodule_dir.iterdir()):
        info(f"Initializing {name} submodule")
        repo_root = bash("git rev-parse --show-toplevel").strip()
        bash(f"git submodule update --init {submodule_dir}", directory=repo_root)

    # Check current tag
    current_tag = bash(
        "git describe --tags --exact-match 2>/dev/null || echo ''",
        directory=submodule_dir,
    ).strip()

    if current_tag == tag:
        info(f"{name} already at {tag}")
        return

    info(f"Checking out {name} {tag} (currently at {current_tag or 'unknown'})")

    # Fetch the tag and checkout
    bash(f"git fetch --depth 1 origin tag {tag}", directory=submodule_dir)
    bash(f"git checkout {tag}", directory=submodule_dir)
    bash("git submodule update --init --recursive --depth 1", directory=submodule_dir)
