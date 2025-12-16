<script>
    let { data } = $props();

    function escapeHtml(str) {
        if (typeof str !== 'string') return String(str);
        return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function getType(value) {
        if (value === null) return 'null';
        if (Array.isArray(value)) return 'array';
        return typeof value;
    }

    function getPreview(value, type) {
        if (type === 'array') {
            return value.length === 0 ? '' : `${value.length} items`;
        }
        const keys = Object.keys(value).slice(0, 3);
        if (keys.length === 0) return '';
        if (keys.length < Object.keys(value).length) {
            return `${keys.join(', ')}, ...`;
        }
        return keys.join(', ');
    }

    function shouldExpand(key, value, isRoot) {
        if (isRoot) return true;
        const type = getType(value);
        if (type !== 'object' && type !== 'array') return false;
        const count = type === 'array' ? value.length : Object.keys(value).length;
        return count <= 3 || key === 'results';
    }
</script>

{#snippet jsonNode(value, key = '', isRoot = false)}
    {@const type = getType(value)}
    {@const isExpandable = type === 'object' || type === 'array'}
    {@const nodeClass = isRoot ? 'json-node root' : 'json-node'}

    <div class={nodeClass}>
        {#if !isExpandable}
            <div class="json-line">
                <span class="json-toggle leaf"></span>
                <span class="json-content">
                    {#if key}<span class="json-key">"{escapeHtml(key)}"</span><span class="json-punctuation">: </span>{/if}
                    {#if type === 'null'}
                        <span class="json-null">null</span>
                    {:else if type === 'string'}
                        <span class="json-string">"{escapeHtml(value)}"</span>
                    {:else if type === 'number'}
                        <span class="json-number">{value}</span>
                    {:else if type === 'boolean'}
                        <span class="json-boolean">{value}</span>
                    {:else}
                        <span class="json-null">{value}</span>
                    {/if}
                </span>
            </div>
        {:else}
            {@const isArray = type === 'array'}
            {@const count = isArray ? value.length : Object.keys(value).length}
            {@const openBracket = isArray ? '[' : '{'}
            {@const closeBracket = isArray ? ']' : '}'}
            {@const preview = getPreview(value, type)}
            {@const expanded = shouldExpand(key, value, isRoot)}

            <details open={expanded}>
                <summary class="json-line expandable">
                    <span class="json-toggle"></span>
                    <span class="json-content">
                        {#if key}<span class="json-key">"{escapeHtml(key)}"</span><span class="json-punctuation">: </span>{/if}
                        <span class="json-punctuation">{openBracket}</span>
                        <span class="json-preview">{escapeHtml(preview)}<span class="json-punctuation">{closeBracket}</span></span>
                    </span>
                </summary>
                <div class="json-children">
                    {#if isArray}
                        {#each value as item}
                            {@render jsonNode(item, '', false)}
                        {/each}
                    {:else}
                        {#each Object.entries(value) as [k, v]}
                            {@render jsonNode(v, k, false)}
                        {/each}
                    {/if}
                </div>
                <div class="json-close">
                    <span class="json-toggle leaf"></span>
                    <span class="json-punctuation">{closeBracket}</span>
                </div>
            </details>
        {/if}
    </div>
{/snippet}

<div class="json-tree">
    {@render jsonNode(data, '', true)}
</div>

<style>
    .json-tree {
        padding: var(--space-sm);
        font-size: var(--font-size-xs);
        line-height: 1.6;
        overflow: auto;
        background: var(--color-bg);
        color: var(--color-text);
        font-family: var(--font-mono);
    }

    .json-node {
        margin-left: 16px;
    }

    .json-node.root {
        margin-left: 0;
    }

    .json-line {
        display: flex;
        align-items: flex-start;
        gap: 4px;
    }

    summary.json-line {
        cursor: pointer;
        list-style: none;
    }

    summary.json-line::-webkit-details-marker {
        display: none;
    }

    summary.json-line:hover {
        background: var(--color-surface-elevated);
    }

    .json-toggle {
        width: 14px;
        height: 14px;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        color: var(--color-text-muted);
        flex-shrink: 0;
        margin-top: 2px;
        user-select: none;
    }

    .json-toggle:hover {
        color: var(--color-accent);
    }

    details > summary .json-toggle::before {
        content: '\25B6';
        font-size: 8px;
    }

    details[open] > summary .json-toggle::before {
        content: '\25BC';
    }

    .json-toggle.leaf {
        visibility: hidden;
    }

    .json-content {
        flex: 1;
        min-width: 0;
    }

    .json-children {
        display: block;
    }

    details:not([open]) .json-children,
    details:not([open]) .json-close {
        display: none;
    }

    details[open] .json-preview {
        display: none;
    }

    .json-close {
        display: flex;
        align-items: flex-start;
        gap: 4px;
    }

    .json-key { color: var(--color-accent); }
    .json-string { color: var(--color-success); }
    .json-number { color: var(--color-warning); }
    .json-boolean { color: #c084fc; }
    .json-null { color: var(--color-text-muted); }
    .json-punctuation { color: var(--color-text-secondary); }
    .json-preview { color: var(--color-text-muted); font-style: italic; }
</style>
