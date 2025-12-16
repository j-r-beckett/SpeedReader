// Canvas rendering utilities for OCR visualization

/**
 * Render the complete visualization to a canvas
 */
export function renderVisualization(ctx, imageData, words, layers, colors) {
    const { width, height } = ctx.canvas;

    // Clear canvas
    ctx.clearRect(0, 0, width, height);

    // Draw image if available
    if (imageData?.image) {
        ctx.drawImage(imageData.image, 0, 0);
    }

    // Draw layers in order
    if (layers['aa-boxes']) {
        renderAxisAlignedBoxes(ctx, words, colors.aaBox);
    }
    if (layers['oriented-boxes']) {
        renderOrientedBoxes(ctx, words, colors.orientedBox);
    }
    if (layers['polygons']) {
        renderPolygons(ctx, words, colors.polygon);
    }
    if (layers['text']) {
        renderTextOverlay(ctx, words, colors.highlight);
    }
}

/**
 * Render axis-aligned bounding boxes
 */
function renderAxisAlignedBoxes(ctx, words, color) {
    ctx.strokeStyle = color;
    ctx.lineWidth = 2;

    for (const word of words) {
        const r = word.boundingBox?.rectangle;
        if (r) {
            ctx.strokeRect(r.x, r.y, r.width, r.height);
        }
    }
}

/**
 * Render oriented (rotated) bounding boxes
 */
function renderOrientedBoxes(ctx, words, color) {
    ctx.strokeStyle = color;
    ctx.lineWidth = 2;

    for (const word of words) {
        const rect = word.boundingBox?.rotatedRectangle;
        if (rect) {
            const corners = getOrientedBoxCorners(rect);
            ctx.beginPath();
            ctx.moveTo(corners[0].x, corners[0].y);
            for (let i = 1; i < corners.length; i++) {
                ctx.lineTo(corners[i].x, corners[i].y);
            }
            ctx.closePath();
            ctx.stroke();
        }
    }
}

/**
 * Render polygon bounding boxes
 */
function renderPolygons(ctx, words, color) {
    ctx.strokeStyle = color;
    ctx.lineWidth = 2;

    for (const word of words) {
        const points = word.boundingBox?.polygon?.points;
        if (points?.length > 0) {
            ctx.beginPath();
            ctx.moveTo(points[0].x, points[0].y);
            for (let i = 1; i < points.length; i++) {
                ctx.lineTo(points[i].x, points[i].y);
            }
            ctx.closePath();
            ctx.stroke();
        }
    }
}

/**
 * Render text overlay with highlights and text
 */
function renderTextOverlay(ctx, words, highlightColor) {
    for (const word of words) {
        const rect = word.boundingBox?.rotatedRectangle;
        if (!rect || !word.text) continue;

        const t = getTextTransform(rect);

        // Save context for rotation
        ctx.save();
        ctx.translate(rect.x, rect.y);
        ctx.rotate(rect.angle);

        // Draw pinched pill highlight
        drawPinchedPillHighlight(ctx, rect.width, rect.height, highlightColor);

        // Draw text with shadow
        const fontSize = getFontSize(rect, word.text.length);
        ctx.font = `bold ${fontSize}px Arial, sans-serif`;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';

        const textX = rect.width / 2;
        const textY = rect.height / 2;

        // Draw shadow layers
        ctx.fillStyle = 'rgba(0, 0, 0, 1)';
        ctx.shadowColor = 'rgba(0, 0, 0, 1)';
        ctx.shadowBlur = 3;
        ctx.shadowOffsetX = 2;
        ctx.shadowOffsetY = 2;
        ctx.fillText(word.text, textX, textY);

        // Second shadow layer for glow
        ctx.shadowColor = 'rgba(0, 0, 0, 0.8)';
        ctx.shadowBlur = 6;
        ctx.shadowOffsetX = 0;
        ctx.shadowOffsetY = 0;
        ctx.fillText(word.text, textX, textY);

        // Draw white text on top
        ctx.shadowColor = 'transparent';
        ctx.shadowBlur = 0;
        ctx.shadowOffsetX = 0;
        ctx.shadowOffsetY = 0;
        ctx.fillStyle = '#ffffff';
        ctx.fillText(word.text, textX, textY);

        ctx.restore();
    }
}

/**
 * Draw pinched pill shape with radial gradient
 */
function drawPinchedPillHighlight(ctx, w, h, color) {
    const r = h / 2;
    const pinch = h * 0.3;
    const midX = w / 2;

    // Create radial gradient
    const gradient = ctx.createRadialGradient(midX, h/2, 0, midX, h/2, Math.max(w, h) / 2);
    gradient.addColorStop(0, colorWithAlpha(color, 0.85));
    gradient.addColorStop(0.4, colorWithAlpha(color, 0.6));
    gradient.addColorStop(0.7, colorWithAlpha(color, 0.3));
    gradient.addColorStop(1, colorWithAlpha(color, 0));

    ctx.fillStyle = gradient;

    // Draw pinched pill path
    ctx.beginPath();
    ctx.moveTo(r, 0);
    ctx.quadraticCurveTo(midX, pinch, w - r, 0);
    ctx.arc(w - r, r, r, -Math.PI / 2, Math.PI / 2);
    ctx.quadraticCurveTo(midX, h - pinch, r, h);
    ctx.arc(r, r, r, Math.PI / 2, -Math.PI / 2);
    ctx.closePath();
    ctx.fill();
}

/**
 * Get corners of an oriented rectangle
 */
function getOrientedBoxCorners(rect) {
    const cos = Math.cos(rect.angle);
    const sin = Math.sin(rect.angle);
    return [
        { x: rect.x, y: rect.y },
        { x: rect.x + rect.width * cos, y: rect.y + rect.width * sin },
        { x: rect.x + rect.width * cos - rect.height * sin, y: rect.y + rect.width * sin + rect.height * cos },
        { x: rect.x - rect.height * sin, y: rect.y + rect.height * cos }
    ];
}

/**
 * Calculate text transform values
 */
function getTextTransform(rect) {
    const cos = Math.cos(rect.angle);
    const sin = Math.sin(rect.angle);
    const centerX = rect.x + rect.width / 2 * cos - rect.height / 2 * sin;
    const centerY = rect.y + rect.width / 2 * sin + rect.height / 2 * cos;
    const angleDeg = rect.angle * 180 / Math.PI;
    return { centerX, centerY, angleDeg };
}

/**
 * Calculate appropriate font size for text
 */
function getFontSize(rect, textLen) {
    const fontSizeByWidth = rect.width / (textLen * 0.55);
    const fontSizeByHeight = rect.height;
    return Math.min(fontSizeByWidth, fontSizeByHeight);
}

/**
 * Convert hex color to rgba with alpha
 */
function colorWithAlpha(hex, alpha) {
    // Handle both #rgb and #rrggbb formats
    let r, g, b;
    if (hex.length === 4) {
        r = parseInt(hex[1] + hex[1], 16);
        g = parseInt(hex[2] + hex[2], 16);
        b = parseInt(hex[3] + hex[3], 16);
    } else {
        r = parseInt(hex.slice(1, 3), 16);
        g = parseInt(hex.slice(3, 5), 16);
        b = parseInt(hex.slice(5, 7), 16);
    }
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}
