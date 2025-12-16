<script>
    let { width, height, time } = $props();
</script>

<div class="stats-bar">
    <div class="stat">
        <span>Image Dimensions:</span>
        <span class="stat__value">{width && height ? `${width}x${height}` : '-'}</span>
    </div>
    <div class="stat">
        <span>Processing Time:</span>
        <span class="stat__value">{time ? `${time}s` : '-'}</span>
    </div>
</div>

<style>
    .stats-bar {
        display: flex;
        gap: var(--space-lg);
        padding: var(--space-sm) var(--space-md);
        background: var(--color-bg);
        border-top: 1px solid var(--color-border);
        font-size: var(--font-size-xs);
        color: var(--color-text-secondary);
    }

    .stat {
        display: flex;
        gap: var(--space-xs);
    }

    .stat__value {
        color: var(--color-text);
        font-weight: 700;
    }
</style>
