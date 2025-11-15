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
    console.print(command, style="yellow", highlight=False)

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
        console.print(line, style="bright_black", end="", highlight=False)

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
    console.print(msg, style="green", highlight=False)


def error(msg):
    console.print(msg, style="red", highlight=False)
