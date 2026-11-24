import { AxeBuilder } from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

test('Home Screen setup uses the original icon, valid launch metadata and reachable iPhone instructions', async ({
  page,
  request,
}) => {
  await page.goto('/');
  const manifestLink = page.locator('link[rel="manifest"]');
  const manifestPath = await manifestLink.getAttribute('href');
  expect(manifestPath).toBe('/manifest.webmanifest');
  const response = await request.get(manifestPath!);
  expect(response.ok()).toBeTruthy();
  expect(response.headers()['content-type']).toMatch(/application\/(manifest\+json|json)/);
  const manifest = await response.json();
  expect(manifest.name).toBe('LexiGo');
  expect(manifest.display).toBe('standalone');
  expect(manifest.start_url).toBe('/');
  expect(manifest.scope).toBe('/');
  for (const icon of manifest.icons as { src: string; sizes: string }[]) {
    const image = await request.get(icon.src);
    expect(image.ok()).toBeTruthy();
    expect(image.headers()['content-type']).toMatch(/image\/png/);
    const data = await image.body();
    expect(data.subarray(0, 8).toString('hex')).toBe('89504e470d0a1a0a');
    expect(`${data.readUInt32BE(16)}x${data.readUInt32BE(20)}`).toBe(icon.sizes);
  }
  await expect(page.locator('meta[name="apple-mobile-web-app-capable"]')).toHaveAttribute(
    'content',
    'yes',
  );
  await expect(page.locator('meta[name="apple-mobile-web-app-title"]')).toHaveAttribute(
    'content',
    'LexiGo',
  );
  const appleIcon = await page.locator('link[rel="apple-touch-icon"]').getAttribute('href');
  const appleImage = await request.get(appleIcon!);
  expect(appleImage.ok()).toBeTruthy();
  const appleBytes = await appleImage.body();
  expect(appleBytes.readUInt32BE(16)).toBe(180);
  expect(appleBytes.readUInt32BE(20)).toBe(180);

  await page.getByRole('link', { name: 'Add LexiGo to your phone' }).click();
  await expect(page).toHaveURL(/\/help#install-app$/);
  const installation = page.locator('#install-app');
  await expect(
    installation.getByRole('heading', { name: 'Keep LexiGo on your Home Screen.' }),
  ).toBeVisible();
  await expect(installation.getByText('Safari', { exact: true })).toBeVisible();
  await expect(installation.getByText('Open as Web App', { exact: true })).toBeVisible();
  await expect(
    installation.getByText('Internet access is required', { exact: false }),
  ).toBeVisible();
  await expect(installation.getByRole('img', { name: 'LexiGo app icon' })).toBeVisible();
  await page.reload();
  await expect(installation.getByRole('heading', { level: 2 })).toBeVisible();
  const accessibility = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();
  expect(accessibility.violations).toEqual([]);
  expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)).toBe(
    false,
  );
});
