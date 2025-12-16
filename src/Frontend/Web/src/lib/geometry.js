export function getOrientedBoxPoints(rect) {
    const cos = Math.cos(rect.angle);
    const sin = Math.sin(rect.angle);
    const corners = [
        { x: rect.x, y: rect.y },
        { x: rect.x + rect.width * cos, y: rect.y + rect.width * sin },
        { x: rect.x + rect.width * cos - rect.height * sin, y: rect.y + rect.width * sin + rect.height * cos },
        { x: rect.x - rect.height * sin, y: rect.y + rect.height * cos }
    ];
    return corners.map(c => `${c.x},${c.y}`).join(' ');
}

export function getTextTransform(rect) {
    const cos = Math.cos(rect.angle);
    const sin = Math.sin(rect.angle);
    const centerX = rect.x + rect.width / 2 * cos - rect.height / 2 * sin;
    const centerY = rect.y + rect.width / 2 * sin + rect.height / 2 * cos;
    const angleDeg = rect.angle * 180 / Math.PI;
    return { centerX, centerY, angleDeg };
}

export function getFontSize(rect, textLen) {
    const fontSizeByWidth = rect.width / (textLen * 0.55);
    const fontSizeByHeight = rect.height;
    return Math.min(fontSizeByWidth, fontSizeByHeight);
}

export function getPinchedPillPath(rect, pinchAmount = 0.3) {
    const w = rect.width;
    const h = rect.height;
    const r = h / 2; // end cap radius
    const pinch = h * pinchAmount; // how much to pinch inward at center
    const midX = w / 2;

    // Path starts at top-left of the straight section
    // Goes clockwise: top edge -> right cap -> bottom edge -> left cap
    return `
        M ${r} 0
        Q ${midX} ${pinch}, ${w - r} 0
        A ${r} ${r} 0 0 1 ${w - r} ${h}
        Q ${midX} ${h - pinch}, ${r} ${h}
        A ${r} ${r} 0 0 1 ${r} 0
        Z
    `.trim();
}
