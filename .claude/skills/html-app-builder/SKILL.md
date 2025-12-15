---
name: html-app-builder
description: Build and modify single-file HTML applications embedded in SpeedReader. Use this skill whenever working on HTML apps in src/Resources/Web/ or src/Frontend/Web/, including creating new apps, modifying existing ones, or styling UI components. Covers the full workflow from development through embedding.
---

# HTML App Builder

Build single-file HTML applications for SpeedReader using vanilla HTML, CSS, and JavaScript.

## Core Principles

1. **Single-file architecture** - All HTML, CSS, and JS in one file
2. **No build step** - No React, no JSX, no compilation
3. **CDN dependencies** - Load libraries from CDNs (jsdelivr, unpkg, cdnjs)
4. **Compact code** - Aim for a few hundred lines max
5. **Server integration** - Apps interact with SpeedReader's HTTP API

## Development Workflow

Always follow this workflow when developing HTML apps:

```
1. Create/edit HTML file in src/Frontend/Web/
2. Build SpeedReader: dotnet build src/Frontend
3. Run SpeedReader:   dotnet run --project src/Frontend -- serve
4. View in Playwright and iterate
5. When done, ensure embedding is configured
```

The build is cached, so rebuilding takes only a few seconds.

### Playwright Usage

Use Playwright MCP to view and test the app:

```
1. browser_navigate to http://localhost:5000/demo (or appropriate route)
2. browser_snapshot to see current state
3. Iterate on HTML based on what you see
4. Rebuild and refresh to see changes
```

Prefer `browser_snapshot` over screenshots for understanding page structure.

## Embedding HTML in SpeedReader

HTML apps are embedded in the SpeedReader binary as resources.

### File Locations

- Source HTML: `src/Frontend/Web/` (actual files)
- Resource symlinks: `src/Resources/Web/` (symlinks to Frontend/Web)
- Embedding config: `src/Resources/Web/EmbeddedWeb.embed.xml`
- C# accessor: `src/Resources/Web/EmbeddedWeb.cs`

### Adding a New HTML App

1. Create the HTML file in `src/Frontend/Web/myapp.html`

2. Create symlink in Resources:
   ```bash
   ln -s ../../Frontend/Web/myapp.html src/Resources/Web/myapp.html
   ```

3. Add to `EmbeddedWeb.embed.xml`:
   ```xml
   <EmbeddedResource Include="$(MSBuildThisFileDirectory)myapp.html" />
   ```

4. Add accessor to `EmbeddedWeb.cs`:
   ```csharp
   private readonly Resource _myapp = new("Web.myapp.html");
   public string MyappHtml => Encoding.UTF8.GetString(_myapp.Bytes);
   ```

5. Wire up the route in the server (if needed)

## Design System

SpeedReader uses the **Terminal Noir** design system - a dark, monospace, cyberpunk-inspired aesthetic. The user should feel like they're using precision software from the future.

**Key characteristics:**
- Dark backgrounds (#121a21 base)
- Monospace typography (JetBrains Mono)
- Cyan accent color (#22d3ee)
- Sharp corners (0-2px radius max)
- Borders instead of shadows
- Generous spacing

For full design system details including color palette, typography, component patterns, and CSS snippets, see [references/design-system.md](references/design-system.md).

## Starter Template

A ready-to-use template following the Terminal Noir design system is available at [assets/template.html](assets/template.html). Copy this as a starting point for new apps.

## Common Patterns

### Fetching from SpeedReader API

```javascript
const response = await fetch('/api/ocr', {
    method: 'POST',
    headers: { 'Content-Type': contentType },
    body: fileData
});
const data = await response.json();
```

### File Input Handling

```javascript
const input = document.getElementById('file-input');
input.addEventListener('change', async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    // Process file...
});
```

### Status Messages

```javascript
function showStatus(message, type) {
    const el = document.getElementById('status');
    el.textContent = message;
    el.className = `status status--${type}`; // success, error, processing
}
```

### Loading States

```css
.loading {
    display: inline-block;
    width: 16px;
    height: 16px;
    border: 2px solid var(--color-border);
    border-top-color: var(--color-accent);
    border-radius: 50%;
    animation: spin 0.8s linear infinite;
}

@keyframes spin {
    to { transform: rotate(360deg); }
}
```
