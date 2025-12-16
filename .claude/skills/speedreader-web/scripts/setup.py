# /// script
# requires-python = ">=3.11"
# dependencies = []
# ///
"""
Setup the speedreader-web skill.
Configures the PreToolUse hook for action expansion.
"""

import json
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent.resolve()
PROJECT_ROOT = SCRIPT_DIR.parent.parent.parent.parent
SETTINGS_FILE = PROJECT_ROOT / ".claude" / "settings.json"

REQUIRED_HOOK = {
    "matcher": "mcp__playwright__browser_run_code",
    "hooks": [
        {
            "type": "command",
            "command": "bash -c 'uv run \"$(git rev-parse --show-toplevel)/.claude/skills/speedreader-web/scripts/resolve_action.py\"'"
        }
    ]
}


def info(msg: str):
    print(f"[info] {msg}", file=sys.stderr)


def main():
    # Load existing settings or start fresh
    if SETTINGS_FILE.exists():
        settings = json.loads(SETTINGS_FILE.read_text())
    else:
        settings = {}

    # Check if hook already configured
    hooks = settings.get("hooks", {})
    pre_tool_use = hooks.get("PreToolUse", [])

    hook_exists = any(
        h.get("matcher") == REQUIRED_HOOK["matcher"]
        for h in pre_tool_use
    )

    if hook_exists:
        info("Hook already configured")
        print("READY")
        return

    # Add the hook
    info("Adding PreToolUse hook...")
    pre_tool_use.append(REQUIRED_HOOK)
    hooks["PreToolUse"] = pre_tool_use
    settings["hooks"] = hooks

    # Write back
    SETTINGS_FILE.parent.mkdir(parents=True, exist_ok=True)
    SETTINGS_FILE.write_text(json.dumps(settings, indent=2) + "\n")
    info(f"Updated {SETTINGS_FILE}")
    info("IMPORTANT: Claude Code must be restarted for changes to take effect. Restart with /exit, /resume")
    print("RESTART_REQUIRED")


if __name__ == "__main__":
    main()
