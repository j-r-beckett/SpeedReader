<script>
    import { layerConfig } from '../lib/layers.js';

    let { layers, onToggle } = $props();
</script>

<div class="layers-panel">
    <div class="layers-panel__header">Layers</div>
    {#each layerConfig as layer}
        <button
            type="button"
            class="layer-item"
            class:active={layers[layer.id]}
            data-layer={layer.id}
            onclick={() => onToggle(layer.id)}
        >
            <div class="layer-swatch"></div>
            <span class="layer-item__name">{layer.name}</span>
        </button>
    {/each}
</div>

<style>
    .layers-panel {
        position: absolute;
        top: var(--space-md);
        right: var(--space-md);
        background: var(--color-surface);
        border: 1px solid var(--color-border);
        border-radius: var(--radius-sm);
        padding: var(--space-sm);
        z-index: 10;
        min-width: 160px;
    }

    .layers-panel__header {
        font-size: var(--font-size-xs);
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.1em;
        color: var(--color-text-secondary);
        padding-bottom: var(--space-xs);
        margin-bottom: var(--space-xs);
        border-bottom: 1px solid var(--color-border-subtle);
    }

    .layer-item {
        display: flex;
        align-items: center;
        gap: var(--space-sm);
        padding: var(--space-xs) 0;
        cursor: pointer;
        user-select: none;
        background: none;
        border: none;
        width: 100%;
        font-family: inherit;
        text-align: left;
    }

    .layer-item:hover {
        color: var(--color-accent);
    }

    .layer-swatch {
        width: 14px;
        height: 14px;
        border-radius: 2px;
        border: 2px solid var(--color-border);
        transition: all 0.15s ease;
    }

    .layer-item.active .layer-swatch {
        border-color: currentColor;
        background: currentColor;
    }

    .layer-item[data-layer="aa-boxes"] { color: var(--color-aa-bbox); }
    .layer-item[data-layer="oriented-boxes"] { color: var(--color-oriented-bbox); }
    .layer-item[data-layer="polygons"] { color: var(--color-polygon); }
    .layer-item[data-layer="text"] { color: var(--color-text-overlay); }

    .layer-item__name {
        font-size: var(--font-size-sm);
        color: var(--color-text);
    }

    .layer-item:hover .layer-item__name {
        color: inherit;
    }
</style>
