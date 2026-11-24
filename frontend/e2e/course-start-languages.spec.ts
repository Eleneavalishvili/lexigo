import { expect, test } from '@playwright/test';
import { registerLearner } from './test-account';

test('all four languages open beginner lessons and level checks', async ({
  page,
  request,
  baseURL,
}) => {
  const inbox = process.env['LEXIGO_E2E_INBOX_PORT'];
  test.skip(!inbox, 'Requires the disposable inbox.');
  test.setTimeout(180_000);
  await registerLearner(page, request, baseURL!, inbox!);

  for (const language of [1, 2, 3, 4]) {
    const course = `10000000-0000-0000-0000-00000000000${language}`;
    for (const mode of ['Start from scratch', 'Find my level']) {
      await page.goto(`/learn/start/${course}`);
      await page.getByRole('button', { name: /20 XP \/ day/ }).click();
      await page.getByRole('button', { name: /Continue/ }).click();
      await page.getByRole('button', { name: new RegExp(mode) }).click();
      await page.getByRole('button', { name: /Let’s begin/ }).click();
      await expect(page, `${mode} must open language ${language}`).toHaveURL(/\/learn\/session\//);
      await expect(page.getByRole('button', { name: 'Check', exact: true })).toBeVisible();
      await expect(page.getByRole('alert')).toHaveCount(0);
    }
  }
});
