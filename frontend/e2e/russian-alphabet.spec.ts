import { expect, test } from '@playwright/test';
import { registerLearner } from './test-account';

const russianCourse = '/learn/10000000-0000-0000-0000-000000000004';

test('a beginner can practise Cyrillic and see the difference between Е and Ё', async ({
  page,
  request,
  baseURL,
}) => {
  const inboxPort = process.env['LEXIGO_E2E_INBOX_PORT'];
  test.skip(!inboxPort, 'Requires the disposable email service.');
  test.setTimeout(150_000);
  await registerLearner(page, request, baseURL!, inboxPort!);
  await page.goto(russianCourse);
  await page.getByRole('link', { name: /Choose my goal and starting point/ }).click();
  await page.getByRole('button', { name: /20 XP \/ day/ }).click();
  await page.getByRole('button', { name: /Continue/ }).click();
  await page.getByRole('button', { name: /Start from scratch/ }).click();
  await page.getByRole('button', { name: /Let’s begin/ }).click();
  await expect(page).toHaveURL(/\/learn\/session\//, { timeout: 20_000 });
  await page.goto(russianCourse);

  const chapter = page.getByRole('region', { name: 'Cyrillic basics' });
  await chapter.locator('summary').filter({ hasText: 'Meet the Cyrillic letters' }).click();
  await chapter.getByRole('button', { name: /Start lesson/ }).click();
  const answers = ['М', 'Т', 'К', 'Н', 'м', 'к', 'Е', 'Е', 'К', 'Н', 'ё', 'М'];
  for (const [index, answer] of answers.entries()) {
    const option = page.getByRole('radio', { name: answer, exact: true });
    const tryIt = page.getByRole('button', { name: /Now try it/ });
    if (index < 4) await expect(tryIt).toBeVisible();
    await expect(option.or(tryIt).first()).toBeVisible();
    if (await tryIt.isVisible()) await tryIt.click();
    await expect(option).toBeEnabled();
    await option.press('Space');
    await expect(option).toBeChecked();
    await page.getByRole('button', { name: 'Check', exact: true }).click();
    if (index === 6) {
      await expect(page.getByRole('heading', { name: 'Incorrect' })).toBeVisible();
      await page.reload();
      await expect(page.locator('.answer-correction strong')).toHaveText('Ё');
    } else {
      await expect(page.getByRole('heading', { name: 'Nice!' })).toBeVisible();
    }
    const feedback = page.locator('app-answer-feedback-panel');
    const nextQuestion = feedback.getByRole('button', { name: /Continue/ });
    await expect(nextQuestion).toBeEnabled();
    await nextQuestion.press('Enter');
    await expect(feedback).toHaveCount(0);
  }
  await page.getByRole('button', { name: /Finish & see results/ }).click();
  await expect(page.locator('app-lesson-result-stats').getByText('11 / 12')).toBeVisible();
});
