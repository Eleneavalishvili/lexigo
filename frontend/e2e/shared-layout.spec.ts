import { expect, test } from '@playwright/test';

test('shared styles load without inline handlers and keep public pages centered', async ({
  page,
}) => {
  const policyErrors: string[] = [];
  page.on('console', (message) => {
    if (
      message.type() === 'error' &&
      /Content Security Policy|Content-Security-Policy/.test(message.text())
    )
      policyErrors.push(message.text());
  });
  for (const [path, selector] of [
    ['/', '.site-header.wrap'],
    ['/contact', '.contact-page.wrap'],
  ]) {
    await page.goto(path);
    await expect(page.locator(selector)).toBeVisible();
    await expect(page.locator(selector)).toHaveCSS('max-width', '1280px');
    await expect(page.locator('link[rel="stylesheet"][onload]')).toHaveCount(0);
    const layout = await page.locator(selector).evaluate((element) => {
      const rect = element.getBoundingClientRect();
      const style = getComputedStyle(element);
      return {
        center: (rect.left + rect.right) / 2,
        viewport: document.documentElement.clientWidth,
        gutter: Number.parseFloat(style.paddingLeft),
        scroll: document.documentElement.scrollWidth,
      };
    });
    expect(Math.abs(layout.center - layout.viewport / 2)).toBeLessThanOrEqual(2);
    expect(layout.gutter).toBeGreaterThanOrEqual(20);
    expect(layout.scroll).toBeLessThanOrEqual(layout.viewport + 1);
  }
  expect(policyErrors).toEqual([]);
});
