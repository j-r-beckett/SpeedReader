export function fitToView(container, imageData) {
    if (!container || !imageData) return { x: 0, y: 0, scale: 1 };

    const containerRect = container.getBoundingClientRect();
    if (containerRect.width === 0 || containerRect.height === 0) {
        return { x: 0, y: 0, scale: 1 };
    }

    const scaleX = containerRect.width / imageData.width;
    const scaleY = containerRect.height / imageData.height;
    const scale = Math.min(scaleX, scaleY, 1) * 0.95;

    const x = (containerRect.width - imageData.width * scale) / 2;
    const y = (containerRect.height - imageData.height * scale) / 2;

    return { x, y, scale };
}

export function zoom(container, transform, factor) {
    if (!container) return transform;
    if (!transform.scale || transform.scale <= 0) {
        transform = { ...transform, scale: 1 };
    }

    const rect = container.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return transform;

    const cx = rect.width / 2;
    const cy = rect.height / 2;

    const newScale = Math.max(0.1, Math.min(10, transform.scale * factor));
    const scaleRatio = newScale / transform.scale;

    return {
        x: cx - (cx - transform.x) * scaleRatio,
        y: cy - (cy - transform.y) * scaleRatio,
        scale: newScale
    };
}

export function applyTransform(transform) {
    return `translate(${transform.x}px, ${transform.y}px) scale(${transform.scale})`;
}
