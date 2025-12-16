<script>
    let { onFile, accept = 'image/*', children, placeholder } = $props();

    let isDragOver = $state(false);

    function handleDrop(e) {
        isDragOver = false;
        const files = e.dataTransfer.files;
        if (files.length > 0) {
            const file = files[0];
            if (accept === '*' || file.type.startsWith(accept.replace('/*', '/'))) {
                onFile(file);
            }
        }
    }

    function handleFileChange(e) {
        const file = e.target.files[0];
        if (file) onFile(file);
    }
</script>

<!-- svelte-ignore a11y_no_static_element_interactions -->
<div
    class="drop-zone"
    ondragenter={(e) => { e.preventDefault(); isDragOver = true; }}
    ondragover={(e) => { e.preventDefault(); isDragOver = true; }}
    ondragleave={(e) => { e.preventDefault(); isDragOver = false; }}
    ondrop={(e) => { e.preventDefault(); handleDrop(e); }}
>
    {#if placeholder}
        <div class="drop-zone__placeholder" class:drag-over={isDragOver}>
            <div class="file-input">
                <label for="file-input" class="file-input__label">Select Image</label>
                <input
                    type="file"
                    id="file-input"
                    {accept}
                    onchange={handleFileChange}
                />
            </div>
            <div class="drop-zone__text">or drop an image here</div>
        </div>
    {:else}
        {@render children?.()}
        <div class="file-input file-input--loaded">
            <label for="file-input-loaded" class="file-input__label">Select Image</label>
            <input
                type="file"
                id="file-input-loaded"
                {accept}
                onchange={handleFileChange}
            />
        </div>
    {/if}
</div>

<style>
    .drop-zone {
        flex: 1;
        position: relative;
        overflow: hidden;
        background: var(--color-bg);
    }

    .drop-zone__placeholder {
        position: absolute;
        inset: 0;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        gap: var(--space-md);
        color: var(--color-text-muted);
        transition: background 0.2s ease;
    }

    .drop-zone__placeholder.drag-over {
        background: var(--color-accent-glow);
    }

    .drop-zone__text {
        font-size: var(--font-size-sm);
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
        background: var(--color-accent);
        border: 1px solid var(--color-accent);
        border-radius: var(--radius-sm);
        color: var(--color-bg);
        font-family: var(--font-mono);
        font-size: var(--font-size-sm);
        font-weight: 700;
        cursor: pointer;
        transition: all 0.15s ease;
    }

    .file-input__label:hover {
        background: var(--color-accent-dim);
        border-color: var(--color-accent-dim);
    }

    .file-input--loaded {
        position: absolute;
        right: var(--space-lg);
        bottom: var(--space-lg);
        z-index: 20;
    }
</style>
