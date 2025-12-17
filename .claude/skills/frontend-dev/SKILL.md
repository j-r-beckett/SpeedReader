---
name: frontend-dev
description: Manages local web server lifecycle and enables web page development with Playwright MCP. Use when developing HTML/CSS/JS, building web pages, viewing web pages, testing UI interactions, verifying renders, debugging frontend issues, or iterating on web design. Covers build, startup, shutdown, and browser-based testing workflows.
---

# Frontend Development

Toolkit for frontend development in the SpeedReader codebase. Handles server lifecycle and delegates browser-based testing to Playwright MCP.

## Setup

Run setup first:

```bash
uv run .claude/skills/frontend-dev/scripts/setup.py
```

Outputs:
- `READY` - Proceed with usage
- `RESTART_REQUIRED` - Ask user to restart Claude Code, then stop and wait

## Development Loop

**All scripts must be run with `uv run`, not `python3`.**

### 1. Edit code

Make changes to frontend code in `src/Frontend/`.

### 2. Reload

```bash
uv run .claude/skills/frontend-dev/scripts/reload.py
```

This builds SpeedReader, starts the server, and waits for health. The server runs in the background with a 15-minute auto-shutdown.

`reload.py` is **fast**. Claude should *never* attempt to short-circuit it.

`reload.py` does a complete, fresh build of the frontend. It is *not necessary* to run **any** build steps manually.

**SpeedReader does not hot reload. Run reload.py after every code change.**

### 3. Interact with Playwright MCP

Use Playwright MCP tools to interact with the page at http://localhost:5050/demo.

For visual regression testing, use `?hideProcessingTime=true` to hide dynamic content:
```
http://localhost:5050/demo?hideProcessingTime=true
```

**Snapshot vs Screenshot:**
- Use `browser_snapshot` for interaction (getting element refs) and existence checks
- Use `browser_take_screenshot` for visual verification - things that look fine in a snapshot are often revealed to be broken when viewed as a screenshot

As a general rule, use snapshots for interaction and screenshots for verification and validation.

*Important*: when looking at screenshots, *be careful*. It's very easy to say "this is fine" when the screenshot is close to the expected result, but different in a visually similar but semantically different way. Example: a rectangle with pointed corners vs rounded corners, small adjustments to the position of an item, the levels of indentation in a list. For cases like this, identify the area of interest, look very carefully at it, and try to identify physically small yet semantically distinctive features.

### 4. Repeat

Edit code -> reload -> verify with Playwright -> repeat until done.

## Actions

Actions are reusable JS scripts. Call them via `browser_run_code`:

```
browser_run_code: code="ACTION:demo_upload"
browser_run_code: code="ACTION:demo_upload:file=test.png"
browser_run_code: code="ACTION:toggle_layer:layer=text"
```

Syntax: `ACTION:name` or `ACTION:name:key=value,key2=value2`

Available actions:
- `demo_upload` - Upload image and wait for OCR. Args: `file`
- `toggle_layer` - Toggle a visualization layer. Args: `layer`

### Writing Actions

Actions are JavaScript files in `scripts/actions/`. Each is an async function receiving `page` and `args`:

```javascript
async (page, args) => {
  const file = args.file || 'hello.png';
  await page.locator('#file-input').setInputFiles(file);
  await page.waitForSelector('.stats-bar', { timeout: 60000 });
}
```

Create or edit actions as needed.

## Fixes

### Screenshots timing out

If `browser_take_screenshot` hangs or times out repeatedly, close and reopen the browser:

```
browser_close
browser_navigate: url="http://localhost:5050/demo"
```

This resets the browser state and typically resolves the issue.
