<script>
    import { getOrientedBoxPoints } from '../../lib/geometry.js';

    let { words, visible } = $props();
</script>

<g class="layer-oriented-boxes" class:layer-hidden={!visible}>
    {#each words as word}
        {#if word.boundingBox?.rotatedRectangle}
            <polygon
                points={getOrientedBoxPoints(word.boundingBox.rotatedRectangle)}
                class="oriented-box"
            />
        {/if}
    {/each}
</g>

<style>
    .layer-oriented-boxes :global(.oriented-box) {
        fill: none;
        stroke: var(--color-oriented-bbox);
        stroke-width: 2;
    }

    .layer-hidden {
        display: none;
    }
</style>
