<script>
    import { getTextTransform, getFontSize, getPinchedPillPath } from '../../lib/geometry.js';

    let { words, visible } = $props();
</script>

<g class="layer-text" class:layer-hidden={!visible}>
    {#each words as word, index}
        {#if word.boundingBox?.rotatedRectangle && word.text}
            {@const rect = word.boundingBox.rotatedRectangle}
            {@const t = getTextTransform(rect)}
            {@const fontSize = getFontSize(rect, word.text.length)}
            <path
                d={getPinchedPillPath(rect)}
                class="ocr-highlight"
                transform="translate({rect.x}, {rect.y}) rotate({t.angleDeg}, 0, 0)"
            />
            <text
                x={t.centerX}
                y={t.centerY}
                class="ocr-text"
                font-size={fontSize}
                text-anchor="middle"
                dominant-baseline="central"
                transform="rotate({t.angleDeg}, {t.centerX}, {t.centerY})"
            >
                {word.text}
                <title>{(word.confidence * 100).toFixed(1)}%</title>
            </text>
        {/if}
    {/each}
</g>

<style>
    .layer-text :global(.ocr-highlight) {
        fill: url(#highlight-gradient);
        pointer-events: none;
    }

    .layer-text :global(.ocr-text) {
        font-family: Arial, sans-serif;
        font-weight: bold;
        fill: #fff;
        filter: drop-shadow(2px 2px 3px rgba(0, 0, 0, 1)) drop-shadow(0 0 6px rgba(0, 0, 0, 0.8));
        pointer-events: none;
    }

    .layer-hidden {
        display: none;
    }
</style>
