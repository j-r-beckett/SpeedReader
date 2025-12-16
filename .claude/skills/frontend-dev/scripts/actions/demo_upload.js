async (page, args) => {
  const file = args.file || 'hello.png';
  await page.locator('#file-input').setInputFiles(file);
  // Wait for stats bar to appear (indicates OCR complete)
  await page.waitForSelector('.stats-bar', { timeout: 60000 });
  await page.waitForTimeout(500);
}
