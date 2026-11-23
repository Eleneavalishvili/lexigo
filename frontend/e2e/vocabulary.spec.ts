import { expect, test } from '@playwright/test';
import { registerLearner } from './test-account';

const inboxPort = process.env['LEXIGO_E2E_INBOX_PORT'];
const spanish = '10000000-0000-0000-0000-000000000001';

test('a learner saves a word and sees its review schedule after reloading', async ({
  page,
  request,
  baseURL,
}) => {
  test.skip(!inboxPort, 'Run with the disposable database and local inbox.');
  test.setTimeout(90_000);
  await registerLearner(page, request, baseURL!, inboxPort!);
  await page.goto('/vocabulary');
  await page.getByRole('button', { name: 'Discover words', exact: true }).click();
  await page.getByLabel('Language', { exact: true }).selectOption(spanish);
  await page.getByLabel('Word or English translation').fill('hola');
  await page.getByRole('button', { name: /Find words/ }).click();
  const [savedWord] = await Promise.all([
    page.waitForResponse(
      (response) =>
        response.request().method() === 'POST' &&
        new URL(response.url()).pathname === '/api/v1/me/saved-words',
    ),
    page.getByRole('button', { name: 'Save hola', exact: true }).click(),
  ]);
  expect(savedWord.status()).toBe(201);
  expect(await savedWord.headerValue('location')).toMatch(
    /\/api\/v1\/me\/saved-words\/[a-f0-9-]{36}$/,
  );
  await expect(page.getByRole('button', { name: 'Remove hola', exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Your saved words' }).click();
  await page.getByLabel('Review schedule').selectOption('due');
  await page.getByRole('button', { name: /Find words/ }).click();
  await page.getByRole('button', { name: 'Review word', exact: true }).click();
  await page.getByLabel('Type the English translation').fill('hello');
  await page.getByRole('button', { name: 'Check answer', exact: true }).click();
  await expect(page.getByText(/Correct!.*Next review/)).toBeVisible();

  await page.reload();
  await page.getByLabel('Review schedule').selectOption('scheduled');
  await page.getByRole('button', { name: /Find words/ }).click();
  await expect(page.getByRole('button', { name: 'Remove hola', exact: true })).toBeVisible();
  await expect(page.getByText(/Review from/)).toBeVisible();

  await page.getByRole('button', { name: 'Manage collections' }).click();
  await page.getByLabel('Course for new collection').selectOption(spanish);
  await page.getByLabel('Collection name').fill('Travel vocabulary');
  const [created] = await Promise.all([
    page.waitForResponse(
      (response) =>
        response.request().method() === 'POST' &&
        new URL(response.url()).pathname === '/api/v1/me/collections',
    ),
    page.getByRole('button', { name: 'Create collection' }).click(),
  ]);
  expect(created.status()).toBe(201);
  expect(await created.headerValue('location')).toMatch(
    /\/api\/v1\/me\/collections\/[a-f0-9-]{36}$/,
  );
  await expect(page.getByRole('option', { name: 'Travel vocabulary (0)' })).toBeAttached();
  await page.reload();
  await page.getByRole('button', { name: 'Manage collections' }).click();
  await expect(page.getByRole('option', { name: 'Travel vocabulary (0)' })).toBeAttached();
});
