import { expect, test } from '@playwright/test';

test('a visitor learns the words before finishing a trial lesson', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  await page.goto('/help');
  await page.getByRole('link', { name: /Contact LexiGo/ }).click();
  await expect(page).toHaveURL(/\/contact$/);
  await expect(page.locator('a[href="mailto:lexigolanguage@gmail.com"]')).toBeVisible();
  await page.goto('/courses');
  for (const language of ['Spanish', 'Italian', 'German', 'Russian']) {
    await expect(page.getByRole('button', { name: new RegExp(language) })).toBeVisible();
  }
  await page.getByRole('button', { name: /Spanish/ }).click();
  await page.getByRole('link', { name: /Try a lesson without signing up/ }).click();
  await expect(page.getByText('New word 1 of', { exact: false })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Check answer' })).toHaveCount(0);

  const wordCount = page.getByText(/^New word \d+ of \d+$/);
  const count = Number((await wordCount.innerText()).split(' ').at(-1));
  for (let word = 1; word < count; word++) {
    await page.getByRole('button', { name: /Next word/ }).click();
  }
  await page.getByRole('button', { name: /Start the questions|Now try it/ }).click();

  for (let question = 1; question <= 3; question++) {
    await expect(page.getByRole('progressbar')).toHaveAccessibleName(`Question ${question} of 3`);
    const check = page.getByRole('button', { name: 'Check answer' });
    await expect(check).toBeDisabled();
    const option = page.getByRole('radio').first();
    await option.press('Space');
    await expect(option).toBeChecked();
    await check.click();
    await expect(
      page.getByRole('heading', { name: /Nicely done|here’s the answer/ }),
    ).toBeVisible();
    await page.getByRole('button', { name: /Continue/ }).click();
  }

  await expect(page.getByRole('heading', { name: 'Your first words, unlocked.' })).toBeVisible();
  await expect(page.getByText('This trial awards no XP.', { exact: false })).toBeVisible();
});
