<script>
    import LayerPanel from './LayerPanel.svelte';
    import PanZoomCanvas from './PanZoomCanvas.svelte';
    import DropZone from './DropZone.svelte';
    import { createLayerState } from '../lib/layers.js';
    import { createShortcutHandler } from '../lib/shortcuts.js';

    let { imageData, ocrData, isProcessing, onFileSelect } = $props();

    let layers = $state(createLayerState());
    let panZoom;

    let words = $derived(ocrData?.[0]?.results || []);
    let hasResults = $derived(!!ocrData && !!imageData);

    // Fit to view when results become available (after layout)
    $effect(() => {
        if (hasResults && panZoom) {
            // Wait for layout to complete before fitting
            requestAnimationFrame(() => panZoom.reset());
        }
    });

    function toggleLayer(layer) {
        layers[layer] = !layers[layer];
    }

    const handleKeydown = createShortcutHandler({
        onZoomIn: () => panZoom?.zoomIn(),
        onZoomOut: () => panZoom?.zoomOut(),
        onReset: () => panZoom?.reset(),
        onToggleLayer: toggleLayer,
        canZoom: () => !!imageData && !!panZoom
    });
</script>

<svelte:window onkeydown={handleKeydown} />

<DropZone onFile={onFileSelect} placeholder={!hasResults}>
    {#if isProcessing}
        <div class="processing-overlay visible">
            <div class="processing-spinner"></div>
            <div>Processing</div>
        </div>
    {/if}

    {#if hasResults}
        <div class="zoom-controls visible">
            <button class="btn btn--sm" onclick={() => panZoom?.zoomIn()} title="Zoom In (+)">+</button>
            <button class="btn btn--sm" onclick={() => panZoom?.zoomOut()} title="Zoom Out (-)">-</button>
            <button class="btn btn--sm" onclick={() => panZoom?.reset()} title="Reset View (R)">Reset</button>
        </div>
    {/if}

    <div class="viz-wrapper" class:active={hasResults}>
        <PanZoomCanvas
            bind:this={panZoom}
            {imageData}
            {words}
            {layers}
        />

        {#if hasResults}
            <LayerPanel {layers} onToggle={toggleLayer} />
        {/if}
    </div>
</DropZone>

<style>
    .processing-overlay {
        position: absolute;
        inset: 0;
        display: none;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        gap: var(--space-md);
        background: var(--color-bg);
        color: var(--color-text-muted);
        font-size: var(--font-size-base);
        z-index: 15;
    }

    .processing-overlay.visible {
        display: flex;
    }

    .processing-spinner {
        width: 32px;
        height: 32px;
        border: 3px solid var(--color-border);
        border-top-color: var(--color-accent);
        border-radius: 50%;
        animation: spin 0.8s linear infinite;
    }

    @keyframes spin { to { transform: rotate(360deg); } }

    .zoom-controls {
        position: absolute;
        top: var(--space-md);
        left: var(--space-md);
        display: none;
        gap: var(--space-xs);
        z-index: 10;
    }

    .zoom-controls.visible {
        display: flex;
    }

    .viz-wrapper {
        position: absolute;
        inset: 0;
        display: none;
    }

    .viz-wrapper.active {
        display: block;
    }
</style>
