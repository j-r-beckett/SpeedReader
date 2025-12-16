export const layerConfig = [
    { id: 'aa-boxes', name: 'Axis-aligned boxes', defaultVisible: false },
    { id: 'oriented-boxes', name: 'Oriented boxes', defaultVisible: true },
    { id: 'polygons', name: 'Polygons', defaultVisible: false },
    { id: 'text', name: 'Recognized text', defaultVisible: false }
];

export function createLayerState() {
    return Object.fromEntries(
        layerConfig.map(layer => [layer.id, layer.defaultVisible])
    );
}
