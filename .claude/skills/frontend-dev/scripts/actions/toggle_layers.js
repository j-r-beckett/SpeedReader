async (page, args) => {
  const layers = {
    a_rect: 'aa-boxes',
    r_rect: 'oriented-boxes',
    polygon: 'polygons',
    text: 'text'
  };

  for (const [param, layerId] of Object.entries(layers)) {
    if (!(param in args)) continue;

    const desired = args[param] === 'true';
    const toggle = page.locator(`[data-layer="${layerId}"]`);
    const current = await toggle.getAttribute('aria-pressed') === 'true';

    if (current !== desired) {
      await toggle.click();
      await page.waitForTimeout(50);
    }
  }
}
