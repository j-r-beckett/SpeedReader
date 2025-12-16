<script>
    import panzoom from 'panzoom';

    let {
        contentWidth = 0,
        contentHeight = 0,
        children
    } = $props();

    let container;
    let svgElement;
    let contentGroup;
    let panzoomInstance = null;
    let isTransforming = $state(false);
    let transformTimeout;

    // Initialize panzoom when component mounts and content is ready
    $effect(() => {
        if (contentGroup && contentWidth > 0 && contentHeight > 0) {
            initPanzoom();
        }
        return () => {
            if (panzoomInstance) {
                panzoomInstance.dispose();
                panzoomInstance = null;
            }
        };
    });

    function initPanzoom() {
        if (panzoomInstance) {
            panzoomInstance.dispose();
        }

        panzoomInstance = panzoom(contentGroup, {
            maxZoom: 10,
            minZoom: 0.1,
            smoothScroll: false,
            zoomDoubleClickSpeed: 1,
            beforeWheel: (e) => {
                // Allow wheel zoom anywhere in container
                return false;
            },
            beforeMouseDown: (e) => {
                // Allow drag from anywhere
                return false;
            },
            onTouch: (e) => {
                // Enable touch support
                return true;
            },
            filterKey: () => true
        });

        // Track transform state for disabling filters during animation
        panzoomInstance.on('transform', () => {
            isTransforming = true;
            clearTimeout(transformTimeout);
            transformTimeout = setTimeout(() => {
                isTransforming = false;
            }, 150);
        });

        // Fit to view initially
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
        if (!panzoomInstance || !container || contentWidth <= 0 || contentHeight <= 0) return;

        const containerRect = container.getBoundingClientRect();
        if (containerRect.width === 0 || containerRect.height === 0) return;

        const scaleX = containerRect.width / contentWidth;
        const scaleY = containerRect.height / contentHeight;
        const scale = Math.min(scaleX, scaleY, 1) * 0.95;

        const x = (containerRect.width - contentWidth * scale) / 2;
        const y = (containerRect.height - contentHeight * scale) / 2;

        panzoomInstance.zoomAbs(0, 0, scale);
        panzoomInstance.moveTo(x, y);
    }
</script>

<svelte:window onresize={() => { if (contentWidth > 0 && contentHeight > 0) reset(); }} />

<div class="pan-zoom-container" bind:this={container}>
    <!-- svelte-ignore a11y_no_static_element_interactions -->
    <svg
        class="pan-zoom-svg"
        class:transforming={isTransforming}
        bind:this={svgElement}
    >
        <g bind:this={contentGroup}>
            {@render children()}
        </g>
    </svg>
</div>

<style>
    .pan-zoom-container {
        position: absolute;
        inset: 0;
        overflow: hidden;
    }

    .pan-zoom-svg {
        width: 100%;
        height: 100%;
        cursor: grab;
    }

    .pan-zoom-svg:active {
        cursor: grabbing;
    }

    .pan-zoom-svg.transforming :global(.ocr-text) {
        filter: none !important;
    }
</style>
