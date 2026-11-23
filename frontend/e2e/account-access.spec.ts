import { expect, test } from '@playwright/test';

test('private learning history requires sign-in', async ({ page }) => {
  await page.goto('/completion-cards');
  await expect(page).toHaveURL(/\/login(?:\?|$)/);
  await expect(page.getByRole('heading', { name: 'Hello again.' })).toBeVisible();

  const password = page.getByLabel('Your password');
  await expect(password).toHaveAttribute('type', 'password');
  await page.getByRole('button', { name: 'Show password' }).click();
  await expect(password).toHaveAttribute('type', 'text');
  await page.getByRole('button', { name: 'Hide password' }).click();
  await expect(password).toHaveAttribute('type', 'password');

  await page.goto('/activity');
  await expect(page).toHaveURL(/\/login(?:\?|$)/);
});
