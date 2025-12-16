<script>
    let { words, visible } = $props();
</script>

<g class="layer-polygons" class:layer-hidden={!visible}>
    {#each words as word}
        {#if word.boundingBox?.polygon?.points?.length > 0}
            <polygon
                points={word.boundingBox.polygon.points.map(p => `${p.x},${p.y}`).join(' ')}
                class="polygon-shape"
            />
        {/if}
    {/each}
</g>

<style>
    .layer-polygons :global(.polygon-shape) {
        fill: none;
        stroke: var(--color-polygon);
        stroke-width: 2;
    }

    .layer-hidden {
        display: none;
    }
</style>
