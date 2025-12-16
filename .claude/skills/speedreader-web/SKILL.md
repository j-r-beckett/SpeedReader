---
name: speedreader-web
description: Handles SpeedReader server lifecycle (build, startup, shutdown) and web page rebuild/refresh. Use when you need to verify a web page works, test UI interactions, or see how a page behaves. Also covers development tasks: creating, modifying, styling, reviewing.
---

# SpeedReader Web

Boot SpeedReader and interact with its web pages using Playwright MCP.

## Setup

*Run setup first*:

```bash
uv run .claude/skills/speedreader-web/scripts/setup.py
```

Outputs:
- `READY` - Proceed with usage
- `RESTART_REQUIRED` - Ask user to restart Claude Code, then stop and wait

*If setup is skipped or a restart is required but not executed, this skill will not work properly*

## Usage

### 1. Boot the server

```bash
uv run .claude/skills/speedreader-web/scripts/boot.py
```

Run this command directly (not in background). The script builds SpeedReader, starts the server, waits for it to become healthy, then exits. The server continues running in the background with a 15-minute auto-shutdown watchdog.

*Important*: SpeedReader does not support hot reload. Claude must run boot to pick up any changes.

Options: `--port`, `--shutdown`.

### 2. Use Playwright MCP

If `mcp__playwright__browser_snapshot` is not available, install Playwright MCP:

```bash
claude mcp add playwright -- npx @playwright/mcp@latest
```

Then ask the user to restart Claude Code.

Use Playwright MCP to interact with the target web page.

### 3. Run actions

Actions are reusable JS scripts. Call them via `browser_run_code`:

```
browser_run_code: code="ACTION:demo_upload"
browser_run_code: code="ACTION:demo_upload:file=test.png"
browser_run_code: code="ACTION:toggle_layer:layer=text"
```

Syntax: `ACTION:name` or `ACTION:name:key=value,key2=value2`

## Writing Actions

Actions are JavaScript files in `scripts/actions/`. Each is an async function receiving `page` and `args`:

```javascript
async (page, args) => {
  const file = args.file || 'hello.png';
  await page.locator('#file-input').setInputFiles(file);
  await page.waitForSelector('#stats-bar[style*="flex"]', { timeout: 60000 });
}
```

Available actions:
- `demo_upload` - Upload image and wait for OCR. Args: `file`
- `toggle_layer` - Toggle a visualization layer. Args: `layer`

Create new actions as needed for repetitive interactions.

## Troubleshooting

### Screenshots timing out

If `browser_take_screenshot` hangs or times out repeatedly, close and reopen the browser:

```
browser_close
browser_navigate: url="http://localhost:5000/demo"
```

This resets the browser state and typically resolves the issue.
