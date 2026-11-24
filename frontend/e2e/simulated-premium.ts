import { expect, type Page } from '@playwright/test';

export async function fundTestCard(page: Page): Promise<void> {
  await page.goto('/billing/cards');
  await page.getByLabel('Card nickname').fill('Study test card');
  await page.getByLabel('Card number (optional)').fill('1234 5678 9012 3456');
  await page.getByRole('button', { name: /Create card/ }).click();
  await page
    .getByLabel('Card', { exact: true })
    .selectOption({ label: 'Study test card · GEL · Active' });
  await page.getByLabel('Amount').fill('20');
  const topUp = page.waitForResponse(
    (response) => response.url().endsWith('/top-ups') && response.ok(),
  );
  await page.getByRole('button', { name: /Add balance/ }).click();
  await topUp;
}

export async function buySimulatedPremium(page: Page): Promise<void> {
  await fundTestCard(page);
  await page.goto('/billing/subscribe');
  await page.getByLabel('Premium period').selectOption('71000000-0000-0000-0000-000000000001');
  await page.getByRole('button', { name: /Review order/ }).click();
  const paymentCard = page.getByLabel('Your GEL card');
  await expect(paymentCard.locator('option').filter({ hasText: 'Study test card' })).toHaveCount(1);
  await paymentCard.selectOption({ index: 1 });
  await page.getByRole('button', { name: /Confirm payment/ }).click();
  await expect(page.getByRole('link', { name: /View your receipt/ })).toBeVisible();
}
