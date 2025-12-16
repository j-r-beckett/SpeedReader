async (page, args) => {
  const layer = args.layer || 'oriented-boxes';
  await page.locator(`[data-layer="${layer}"]`).click();
  await page.waitForTimeout(100);
}
