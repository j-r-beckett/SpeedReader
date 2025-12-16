# /// script
# requires-python = ">=3.11"
# dependencies = []
# ///
"""
Resolve an action name to its JavaScript code.
Called by the PreToolUse hook to expand ACTION:name patterns.

Syntax: ACTION:name or ACTION:name:key=value,key2=value2

Input (stdin): JSON with tool_name, tool_input
Output (stdout): JSON with hookSpecificOutput containing updatedInput
"""

import json
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent.resolve()
ACTIONS_DIR = SCRIPT_DIR / "actions"


def parse_action_string(action_str: str) -> tuple[str, dict]:
    """Parse 'name:key=value,key2=value2' into (name, {key: value, ...})."""
    if ':' not in action_str:
        return action_str, {}

    parts = action_str.split(':', 1)
    name = parts[0]
    args = {}

    if parts[1]:
        for arg in parts[1].split(','):
            if '=' in arg:
                key, value = arg.split('=', 1)
                args[key.strip()] = value.strip()

    return name, args


def debug(msg: str):
    print(f"[resolve_action] {msg}", file=sys.stderr)


def main():
    debug("Hook triggered")
    input_data = json.load(sys.stdin)
    debug(f"Input: {input_data}")

    tool_name = input_data.get("tool_name", "")
    tool_input = input_data.get("tool_input", {})

    # Only intercept browser_run_code from playwright MCP
    if "browser_run_code" not in tool_name:
        sys.exit(0)

    code = tool_input.get("code", "")

    # Check for ACTION:name pattern
    if not code.startswith("ACTION:"):
        sys.exit(0)

    action_str = code[7:].strip()  # Remove "ACTION:" prefix
    action_name, args = parse_action_string(action_str)
    action_file = ACTIONS_DIR / f"{action_name}.js"

    if not action_file.exists():
        available = [f.stem for f in ACTIONS_DIR.glob("*.js")]
        print(json.dumps({
            "hookSpecificOutput": {
                "hookEventName": "PreToolUse",
                "permissionDecision": "deny",
                "message": f"Action '{action_name}' not found. Available: {', '.join(available) or 'none'}"
            }
        }))
        sys.exit(0)

    # Read action code
    action_code = action_file.read_text()

    # Wrap action with args
    args_json = json.dumps(args)
    wrapped_code = f"""async (page) => {{
  const args = {args_json};
  const action = {action_code};
  await action(page, args);
}}"""

    # Return updated input with wrapped code
    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": "allow",
            "updatedInput": {
                "code": wrapped_code
            }
        }
    }))


if __name__ == "__main__":
    main()
