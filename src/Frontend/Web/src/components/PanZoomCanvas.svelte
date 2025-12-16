<script>
    import panzoom from 'panzoom';
    import { renderVisualization } from '../lib/canvas-renderer.js';

    let {
        imageData = null,
        words = [],
        layers = {}
    } = $props();

    const colors = {
        aaBox: '#f97316',
        orientedBox: '#22d3ee',
        polygon: '#a855f7',
        highlight: '#22d3ee'
    };

    let container;
    let canvas;
    let panzoomInstance = null;

    // Render when data or layers change
    $effect(() => {
        if (canvas && imageData?.image) {
            const layersCopy = { ...layers };
            render(layersCopy);
        }
    });

    // Initialize panzoom when canvas and image are ready
    $effect(() => {
        if (canvas && imageData?.image && !panzoomInstance) {
            initPanzoom();
        }
    });

    // Cleanup on destroy
    $effect(() => {
        return () => {
            if (panzoomInstance) {
                panzoomInstance.dispose();
                panzoomInstance = null;
            }
        };
    });

    function render(currentLayers) {
        canvas.width = imageData.width;
        canvas.height = imageData.height;
        const ctx = canvas.getContext('2d');
        renderVisualization(ctx, imageData, words, currentLayers, colors);
    }

    function initPanzoom() {
        panzoomInstance = panzoom(canvas, {
            maxZoom: 10,
            minZoom: 0.1,
            smoothScroll: false,
            zoomDoubleClickSpeed: 1,
            beforeWheel: () => false,
            beforeMouseDown: () => false,
            filterKey: () => true
        });
        reset();
    }

    export function zoomIn() {
        if (!panzoomInstance || !container) return;
        const rect = container.getBoundingClientRect();
        panzoomInstance.smoothZoom(rect.width / 2, rect.height / 2, 1.25);
    }

    export function zoomOut() {
        if (!panzoomInstance || !container) return;
        const rect = container.getBoundingClientRect();
        panzoomInstance.smoothZoom(rect.width / 2, rect.height / 2, 0.8);
    }

    export function reset() {
        if (!panzoomInstance || !container || !imageData) return;

        const containerRect = container.getBoundingClientRect();
        if (containerRect.width === 0 || containerRect.height === 0) return;

        const scaleX = containerRect.width / imageData.width;
        const scaleY = containerRect.height / imageData.height;
        const scale = Math.min(scaleX, scaleY, 1) * 0.95;

        const x = (containerRect.width - imageData.width * scale) / 2;
        const y = (containerRect.height - imageData.height * scale) / 2;

        panzoomInstance.zoomAbs(0, 0, scale);
        panzoomInstance.moveTo(x, y);
    }
</script>

<svelte:window onresize={() => { if (imageData) reset(); }} />

<div class="pan-zoom-container" bind:this={container}>
    <canvas bind:this={canvas} class="pan-zoom-canvas"></canvas>
</div>

<style>
    .pan-zoom-container {
        position: absolute;
        inset: 0;
        overflow: hidden;
    }

    .pan-zoom-canvas {
        cursor: grab;
    }

    .pan-zoom-canvas:active {
        cursor: grabbing;
    }
</style>
