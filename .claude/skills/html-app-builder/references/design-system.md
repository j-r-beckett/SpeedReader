# Terminal Noir Design System

SpeedReader's visual identity. A precision instrument from 2087 - clean, functional, slightly dangerous.

## Philosophy

- **Dark and focused** - Reduces eye strain, emphasizes content
- **Monospace everything** - Technical tool deserves technical typography
- **Sharp edges** - No soft, friendly curves. This is serious software.
- **Subtle futurism** - Hints of cyberpunk without going overboard

## Color Palette

### CSS Variables

```css
:root {
    /* Backgrounds */
    --color-bg: #121a21;
    --color-surface: #1a252e;
    --color-surface-elevated: #243240;

    /* Borders */
    --color-border: #1e3a4d;
    --color-border-subtle: #162530;

    /* Text */
    --color-text: #e2e8f0;
    --color-text-secondary: #64748b;
    --color-text-muted: #475569;

    /* Accent */
    --color-accent: #22d3ee;
    --color-accent-dim: #0891b2;
    --color-accent-glow: rgba(34, 211, 238, 0.15);

    /* Semantic */
    --color-success: #10b981;
    --color-warning: #f59e0b;
    --color-error: #ef4444;

    /* Spacing */
    --space-xs: 4px;
    --space-sm: 8px;
    --space-md: 16px;
    --space-lg: 24px;
    --space-xl: 32px;
    --space-2xl: 48px;

    /* Typography */
    --font-mono: 'JetBrains Mono', 'Fira Code', 'Consolas', 'Monaco', monospace;
    --font-size-xs: 11px;
    --font-size-sm: 13px;
    --font-size-base: 15px;
    --font-size-lg: 18px;
    --font-size-xl: 24px;
    --font-size-2xl: 32px;

    /* Borders */
    --radius-none: 0px;
    --radius-sm: 2px;
    --radius-md: 4px;  /* Maximum - use sparingly */
}
```

### Color Reference

| Token | Hex | Usage |
|-------|-----|-------|
| `--color-bg` | #121a21 | Page background |
| `--color-surface` | #1a252e | Cards, panels, containers |
| `--color-surface-elevated` | #243240 | Dropdowns, modals, tooltips |
| `--color-border` | #1e3a4d | Primary borders |
| `--color-border-subtle` | #162530 | Subtle separators |
| `--color-text` | #e2e8f0 | Primary text |
| `--color-text-secondary` | #64748b | Labels, hints, metadata |
| `--color-text-muted` | #475569 | Disabled, placeholder |
| `--color-accent` | #22d3ee | Interactive elements, links, focus |
| `--color-accent-dim` | #0891b2 | Hover states, secondary accent |
| `--color-success` | #10b981 | Success states, valid, complete |
| `--color-warning` | #f59e0b | Warnings, caution |
| `--color-error` | #ef4444 | Errors, destructive actions |

## Typography

### Base Styles

```css
body {
    font-family: var(--font-mono);
    font-size: var(--font-size-base);
    line-height: 1.6;
    color: var(--color-text);
    background: var(--color-bg);
    -webkit-font-smoothing: antialiased;
}
```

### Headings

```css
h1, h2, h3, h4 {
    font-weight: 700;
    line-height: 1.3;
    color: var(--color-text);
    margin: 0 0 var(--space-md) 0;
}

h1 { font-size: var(--font-size-2xl); }
h2 { font-size: var(--font-size-xl); }
h3 { font-size: var(--font-size-lg); }
h4 { font-size: var(--font-size-base); }
```

### Section Labels

Use uppercase with letter-spacing for section headers:

```css
.section-label {
    font-size: var(--font-size-xs);
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.1em;
    color: var(--color-text-secondary);
}
```

## Components

### Buttons

```css
/* Base button */
.btn {
    font-family: var(--font-mono);
    font-size: var(--font-size-sm);
    font-weight: 700;
    padding: var(--space-sm) var(--space-md);
    border: 1px solid var(--color-border);
    border-radius: var(--radius-sm);
    background: transparent;
    color: var(--color-text);
    cursor: pointer;
    transition: all 0.15s ease;
}

.btn:hover {
    border-color: var(--color-accent);
    color: var(--color-accent);
}

.btn:focus {
    outline: none;
    border-color: var(--color-accent);
    box-shadow: 0 0 0 3px var(--color-accent-glow);
}

/* Primary button */
.btn--primary {
    background: var(--color-accent);
    border-color: var(--color-accent);
    color: var(--color-bg);
}

.btn--primary:hover {
    background: var(--color-accent-dim);
    border-color: var(--color-accent-dim);
    color: var(--color-bg);
}

/* Danger button */
.btn--danger {
    border-color: var(--color-error);
    color: var(--color-error);
}

.btn--danger:hover {
    background: var(--color-error);
    color: var(--color-bg);
}
```

### Cards / Panels

```css
.card {
    background: var(--color-surface);
    border: 1px solid var(--color-border);
    border-radius: var(--radius-sm);
    padding: var(--space-lg);
}

.card__header {
    font-size: var(--font-size-xs);
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.1em;
    color: var(--color-text-secondary);
    margin-bottom: var(--space-md);
    padding-bottom: var(--space-sm);
    border-bottom: 1px solid var(--color-border-subtle);
}
```

### Inputs

```css
.input {
    font-family: var(--font-mono);
    font-size: var(--font-size-base);
    padding: var(--space-sm) var(--space-md);
    background: var(--color-bg);
    border: 1px solid var(--color-border);
    border-radius: var(--radius-sm);
    color: var(--color-text);
    width: 100%;
    box-sizing: border-box;
    transition: border-color 0.15s ease, box-shadow 0.15s ease;
}

.input::placeholder {
    color: var(--color-text-muted);
}

.input:focus {
    outline: none;
    border-color: var(--color-accent);
    box-shadow: 0 0 0 3px var(--color-accent-glow);
}

.input:disabled {
    opacity: 0.5;
    cursor: not-allowed;
}
```

### File Input (Custom)

```css
.file-input {
    position: relative;
    display: inline-block;
}

.file-input input[type="file"] {
    position: absolute;
    left: -9999px;
}

.file-input__label {
    display: inline-flex;
    align-items: center;
    gap: var(--space-sm);
    padding: var(--space-sm) var(--space-md);
    background: var(--color-surface);
    border: 1px solid var(--color-border);
    border-radius: var(--radius-sm);
    color: var(--color-text);
    cursor: pointer;
    transition: all 0.15s ease;
}

.file-input__label:hover {
    border-color: var(--color-accent);
    color: var(--color-accent);
}
```

### Status Messages

```css
.status {
    font-size: var(--font-size-sm);
    padding: var(--space-sm) var(--space-md);
    border-radius: var(--radius-sm);
}

.status--success {
    color: var(--color-success);
    background: rgba(16, 185, 129, 0.1);
    border: 1px solid rgba(16, 185, 129, 0.3);
}

.status--error {
    color: var(--color-error);
    background: rgba(239, 68, 68, 0.1);
    border: 1px solid rgba(239, 68, 68, 0.3);
}

.status--warning {
    color: var(--color-warning);
    background: rgba(245, 158, 11, 0.1);
    border: 1px solid rgba(245, 158, 11, 0.3);
}

.status--processing {
    color: var(--color-accent);
}
```

### Tables

```css
.table {
    width: 100%;
    border-collapse: collapse;
    font-size: var(--font-size-sm);
}

.table th,
.table td {
    padding: var(--space-sm) var(--space-md);
    text-align: left;
    border-bottom: 1px solid var(--color-border-subtle);
}

.table th {
    font-size: var(--font-size-xs);
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: var(--color-text-secondary);
    background: var(--color-bg);
}

.table tr:hover td {
    background: var(--color-surface-elevated);
}
```

### Code Blocks

```css
.code {
    font-family: var(--font-mono);
    font-size: var(--font-size-sm);
    padding: var(--space-md);
    background: var(--color-bg);
    border: 1px solid var(--color-border);
    border-radius: var(--radius-sm);
    overflow-x: auto;
}

.code--inline {
    display: inline;
    padding: 2px 6px;
    font-size: 0.9em;
}
```

## Layout

### Container

```css
.container {
    max-width: 1200px;
    margin: 0 auto;
    padding: var(--space-lg);
}
```

### Grid

```css
.grid {
    display: grid;
    gap: var(--space-lg);
}

.grid--2col {
    grid-template-columns: repeat(2, 1fr);
}

.grid--3col {
    grid-template-columns: repeat(3, 1fr);
}

@media (max-width: 768px) {
    .grid--2col,
    .grid--3col {
        grid-template-columns: 1fr;
    }
}
```

### Flex Utilities

```css
.flex { display: flex; }
.flex--between { justify-content: space-between; }
.flex--center { align-items: center; }
.flex--gap-sm { gap: var(--space-sm); }
.flex--gap-md { gap: var(--space-md); }
```

## Cyberpunk Touches

Use these sparingly for atmosphere.

### Scanline Overlay

Apply to hero sections or full-page backgrounds:

```css
.scanlines::after {
    content: '';
    position: absolute;
    inset: 0;
    background: repeating-linear-gradient(
        0deg,
        transparent,
        transparent 2px,
        rgba(0, 0, 0, 0.1) 2px,
        rgba(0, 0, 0, 0.1) 4px
    );
    pointer-events: none;
}
```

### Glow Effect

For focus states or important elements:

```css
.glow {
    box-shadow:
        0 0 10px var(--color-accent-glow),
        0 0 20px var(--color-accent-glow),
        0 0 30px var(--color-accent-glow);
}
```

### Blinking Cursor

For active input indication:

```css
.cursor-blink::after {
    content: '▋';
    color: var(--color-accent);
    animation: blink 1s step-end infinite;
}

@keyframes blink {
    50% { opacity: 0; }
}
```

### List Markers

Use `▸` instead of bullets:

```css
.list {
    list-style: none;
    padding: 0;
}

.list li {
    padding-left: var(--space-md);
    position: relative;
}

.list li::before {
    content: '▸';
    position: absolute;
    left: 0;
    color: var(--color-accent);
}
```

## What NOT to Do

- **No rounded corners > 4px** - Breaks the sharp aesthetic
- **No shadows** - Use borders and glows instead
- **No neon pink/purple** - Too vaporwave, we're going for cyberpunk
- **No glitch effects** - Distracting and overdone
- **No excessive glow** - One or two glowing elements max per view
- **No sans-serif fonts** - Stick to monospace
- **No gradients** - Flat colors only (except for subtle scanlines)
- **No white backgrounds** - Always use dark colors
