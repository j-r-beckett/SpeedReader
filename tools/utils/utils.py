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


def ensure_repo(repo_dir: Path, url: str, tag: str, name: str = None):
    """
    Ensure a git repository is cloned and checked out at a specific tag.

    Args:
        repo_dir: Path where the repo should be cloned
        url: Git URL to clone from
        tag: Git tag to checkout
        name: Human-readable name for logging (defaults to directory name)
    """
    import shutil

    if name is None:
        name = repo_dir.name

    def needs_clone():
        """Check if repo needs to be cloned."""
        if not repo_dir.exists():
            return True
        if not any(repo_dir.iterdir()):
            return True

        git_dir = repo_dir / ".git"

        # No .git = not a git repo
        if not git_dir.exists():
            info(f"{name}: not a git repo, will clone fresh")
            shutil.rmtree(repo_dir)
            return True

        # .git is a file (submodule reference) = broken state, clone fresh
        if git_dir.is_file():
            info(f"{name}: has submodule .git file, will clone fresh")
            shutil.rmtree(repo_dir)
            return True

        return False

    if needs_clone():
        info(f"Cloning {name}")
        repo_dir.parent.mkdir(parents=True, exist_ok=True)
        bash(
            f"GIT_TERMINAL_PROMPT=0 git clone --depth 1 --branch {tag} {url} {repo_dir}",
            directory=repo_dir.parent,
        )
        # Initialize nested submodules
        bash(
            "GIT_TERMINAL_PROMPT=0 git submodule update --init --recursive --depth 1",
            directory=repo_dir,
        )
        info(f"{name} cloned at {tag}")
        return

    # Check current tag
    current_tag = bash(
        "git describe --tags --exact-match 2>/dev/null || echo ''",
        directory=repo_dir,
    ).strip()

    if current_tag == tag:
        info(f"{name} already at {tag}")
        return

    info(f"Checking out {name} {tag} (currently at {current_tag or 'unknown'})")

    # Fetch the tag and checkout
    bash(f"GIT_TERMINAL_PROMPT=0 git fetch --depth 1 origin tag {tag}", directory=repo_dir)
    bash(f"git checkout {tag}", directory=repo_dir)
    bash("GIT_TERMINAL_PROMPT=0 git submodule update --init --recursive --depth 1", directory=repo_dir)
