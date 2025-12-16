export async function runOcr(file) {
    const response = await fetch('/api/ocr', {
        method: 'POST',
        headers: { 'Content-Type': file.type },
        body: file
    });

    if (!response.ok) {
        throw new Error(await response.text() || `HTTP ${response.status}`);
    }

    const data = await response.json();

    if (!Array.isArray(data) || data.length === 0) {
        throw new Error('No OCR results returned');
    }

    return data;
}

export function loadImageData(file) {
    return new Promise((resolve) => {
        const reader = new FileReader();
        reader.onload = (ev) => {
            const img = new Image();
            img.onload = () => {
                resolve({
                    width: img.width,
                    height: img.height,
                    dataUrl: ev.target.result,
                    image: img  // Include actual Image object for canvas rendering
                });
            };
            img.src = ev.target.result;
        };
        reader.readAsDataURL(file);
    });
}
