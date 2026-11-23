import { expect, test } from '@playwright/test';
import { registerLearner } from './test-account';

const course = '10000000-0000-0000-0000-000000000004';

test('Russian placement starts and resumes with the same saved answer', async ({
  page,
  request,
  baseURL,
}) => {
  const inboxPort = process.env['LEXIGO_E2E_INBOX_PORT'];
  test.skip(!inboxPort, 'Requires the disposable inbox.');
  test.setTimeout(110_000);
  await registerLearner(page, request, baseURL!, inboxPort!);
  await page.goto(`/learn/${course}`);
  await page.getByRole('link', { name: /Choose my goal and starting point/ }).click();
  await page.getByRole('button', { name: /20 XP \/ day/ }).click();
  await page.getByRole('button', { name: /Continue/ }).click();
  await page.getByRole('button', { name: /Find my level/ }).click();
  const waitForPlacement = () =>
    page.waitForResponse(
      (response) =>
        response.request().method() === 'POST' &&
        new URL(response.url()).pathname === `/api/v1/courses/${course}/placement-sessions`,
    );
  const [created, enrolled] = await Promise.all([
    waitForPlacement(),
    page.waitForResponse(
      (response) =>
        response.request().method() === 'POST' &&
        new URL(response.url()).pathname === '/api/v1/me/enrollments',
    ),
    page.getByRole('button', { name: /Let’s begin/ }).click(),
  ]);
  expect(enrolled.status()).toBe(201);
  expect(await enrolled.headerValue('location')).toMatch(
    new RegExp(`/api/v1/me/enrollments/${course}$`),
  );
  expect(created.status()).toBe(201);
  const { id } = (await created.json()) as { id: string };
  expect(await created.headerValue('location')).toMatch(
    new RegExp(`/api/v1/me/learning-sessions/${id}$`),
  );
  await expect(page).toHaveURL(new RegExp(`/learn/session/${id}$`));
  const choice = page.getByRole('radio').first();
  const text = page.getByLabel('Your translation');
  await expect(choice.or(text)).toBeVisible();
  if (await choice.isVisible()) await choice.press('Space');
  else await text.fill('Мой ответ');
  await page.getByRole('button', { name: 'Check', exact: true }).click();
  await expect(page.getByRole('progressbar', { name: /1 of \d+ answers saved/ })).toBeVisible();

  await page.goto(`/learn/${course}`);
  await page.getByText('Practice and placement tools', { exact: true }).click();
  const [resumed] = await Promise.all([
    waitForPlacement(),
    page.getByRole('button', { name: /Find my starting point/ }).click(),
  ]);
  expect(resumed.status()).toBe(200);
  await expect(page).toHaveURL(new RegExp(`/learn/session/${id}$`));
  await expect(page.getByRole('progressbar', { name: /1 of \d+ answers saved/ })).toBeVisible();
});
