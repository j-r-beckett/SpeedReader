import { layerConfig } from './layers.js';

export function createShortcutHandler({ onZoomIn, onZoomOut, onReset, onToggleLayer, canZoom }) {
    const layerShortcuts = Object.fromEntries(
        layerConfig.map((layer, index) => [String(index + 1), () => onToggleLayer(layer.id)])
    );

    const shortcuts = {
        'r': () => canZoom() && onReset(),
        'R': () => canZoom() && onReset(),
        '=': () => canZoom() && onZoomIn(),
        '+': () => canZoom() && onZoomIn(),
        '-': () => canZoom() && onZoomOut(),
        '_': () => canZoom() && onZoomOut(),
        ...layerShortcuts
    };

    return function handleKeydown(e) {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;

        const handler = shortcuts[e.key];
        if (handler) {
            e.preventDefault();
            handler();
        }
    };
}
