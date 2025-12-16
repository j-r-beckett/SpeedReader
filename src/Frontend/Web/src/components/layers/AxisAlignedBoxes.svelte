<script>
    let { words, visible } = $props();
</script>

<g class="layer-aa-boxes" class:layer-hidden={!visible}>
    {#each words as word}
        {#if word.boundingBox?.rectangle}
            {@const r = word.boundingBox.rectangle}
            <rect
                x={r.x}
                y={r.y}
                width={r.width}
                height={r.height}
                class="aa-box"
            />
        {/if}
    {/each}
</g>

<style>
    .layer-aa-boxes :global(.aa-box) {
        fill: none;
        stroke: var(--color-aa-bbox);
        stroke-width: 2;
    }

    .layer-hidden {
        display: none;
    }
</style>
