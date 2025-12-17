<script>
    import Visualization from './components/Visualization.svelte';
    import JsonTree from './components/JsonTree.svelte';
    import StatsBar from './components/StatsBar.svelte';
    import { runOcr, loadImageData } from './lib/api.js';

    const urlParams = new URLSearchParams(window.location.search);
    const hideProcessingTime = urlParams.get('hideProcessingTime') === 'true';

    let ocrData = $state(null);
    let imageData = $state(null);
    let isProcessing = $state(false);
    let processingTime = $state(null);

    async function handleFile(file) {
        if (!file || !file.type.startsWith('image/')) return;

        isProcessing = true;
        const startTime = performance.now();

        loadImageData(file).then(data => { imageData = data; });

        try {
            ocrData = await runOcr(file);
            processingTime = ((performance.now() - startTime) / 1000).toFixed(2);
        } catch (err) {
            console.error('OCR error:', err);
        } finally {
            isProcessing = false;
        }
    }

    async function handleCopy() {
        if (!ocrData) return;
        try {
            await navigator.clipboard.writeText(JSON.stringify(ocrData, null, 2));
        } catch (err) {
            console.error('Copy failed:', err);
        }
    }
</script>

<div class="app">
    <header class="header">
        <h1 class="header__title">SpeedReader</h1>
    </header>

    <main class="main">
        <div class="viz-panel">
            <Visualization
                {imageData}
                {ocrData}
                {isProcessing}
                onFileSelect={handleFile}
            />
        </div>

        <div class="side-panel">
            <div class="panel-section json-panel">
                <div class="panel-header">
                    <span class="panel-header__label">JSON Output</span>
                    <div class="panel-header__actions">
                        <button class="btn btn--sm" onclick={handleCopy}>Copy</button>
                    </div>
                </div>
                <div class="panel-content">
                    {#if ocrData}
                        <JsonTree data={ocrData} />
                    {:else}
                        <div class="empty-state">No results</div>
                    {/if}
                </div>
            </div>
        </div>
    </main>

    {#if ocrData}
        <StatsBar
            width={imageData?.width}
            height={imageData?.height}
            time={hideProcessingTime ? null : processingTime}
        />
    {/if}
</div>

<style>
    .app {
        display: flex;
        flex-direction: column;
        height: 100vh;
    }

    .header {
        padding: var(--space-md) var(--space-lg);
        background: var(--color-surface);
        border-bottom: 1px solid var(--color-border);
        flex-shrink: 0;
    }

    .header__title {
        font-size: var(--font-size-lg);
        font-weight: 700;
    }

    .main {
        display: flex;
        flex: 1;
        min-height: 0;
    }

    .viz-panel {
        flex: 1;
        display: flex;
        flex-direction: column;
        min-width: 0;
        border-right: 1px solid var(--color-border);
    }

    .side-panel {
        width: 380px;
        display: flex;
        flex-direction: column;
        background: var(--color-surface);
        flex-shrink: 0;
    }

    .panel-section {
        display: flex;
        flex-direction: column;
        border-bottom: 1px solid var(--color-border);
    }

    .panel-section:last-child {
        border-bottom: none;
        flex: 1;
        min-height: 0;
    }

    .panel-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: var(--space-sm) var(--space-md);
        background: var(--color-bg);
        border-bottom: 1px solid var(--color-border-subtle);
    }

    .panel-header__label {
        font-size: var(--font-size-xs);
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.1em;
        color: var(--color-text-secondary);
    }

    .panel-header__actions {
        display: flex;
        gap: var(--space-xs);
    }

    .panel-content {
        flex: 1;
        overflow-y: auto;
        min-height: 0;
    }

    .json-panel {
        flex: 1;
        display: flex;
        flex-direction: column;
        min-height: 200px;
    }

    .empty-state {
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        padding: var(--space-xl);
        color: var(--color-text-muted);
        text-align: center;
    }

    @media (max-width: 900px) {
        .main {
            flex-direction: column;
        }
        .viz-panel {
            border-right: none;
            border-bottom: 1px solid var(--color-border);
            min-height: 300px;
        }
        .side-panel {
            width: 100%;
        }
    }
</style>
