import { expect, test } from '@playwright/test';
import { registerLearner } from './test-account';

test('catalog chapter failures can be retried and empty lessons have a useful explanation', async ({
  page,
}) => {
  await page.route(
    '**/api/v1/courses/*/units',
    (route) => route.fulfill({ status: 503, json: { title: 'Temporarily unavailable' } }),
    { times: 1 },
  );
  await page.goto('/courses');
  await page.getByRole('button', { name: /Spanish/ }).click();
  await expect(page.getByRole('alert')).toContainText('couldn’t open');
  await page.getByRole('button', { name: /Try again/ }).click();
  const chapters = page.getByRole('navigation', { name: 'Course units' }).getByRole('button');
  await expect(chapters.first()).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
  await page.route('**/api/v1/units/*/lessons', (route) => route.fulfill({ json: [] }), {
    times: 1,
  });
  await chapters.first().click();
  await expect(page.getByRole('status')).toHaveText(
    'No lessons are published in this chapter yet. Choose another chapter.',
  );
  await chapters.nth(1).click();
  await expect(page.locator('.lesson-list button').first()).toBeVisible();
  await expect(page.getByText('No lessons are published', { exact: false })).toHaveCount(0);
});

test('course setup recovers from a failed read before allowing lesson enrollment', async ({
  page,
  request,
  baseURL,
}) => {
  const inbox = process.env['LEXIGO_E2E_INBOX_PORT'];
  test.skip(!inbox, 'Requires disposable email and learner services.');
  test.setTimeout(90_000);
  await registerLearner(page, request, baseURL!, inbox!);
  await page.goto('/account');
  await page.route(
    '**/api/v1/me/progress?*',
    (route) => route.fulfill({ status: 503, json: { title: 'Temporarily unavailable' } }),
    { times: 1 },
  );
  await page.goto('/learn/start/10000000-0000-0000-0000-000000000001');
  await expect(page.getByRole('alert')).toContainText('This course could not be opened');
  const goal = page.getByRole('button', { name: /20 XP \/ day/ });
  await expect(goal).toBeDisabled();
  await expect(page.getByRole('button', { name: /Continue/ })).toBeDisabled();
  await page.getByRole('button', { name: 'Try loading course again', exact: true }).click();
  await expect(goal).toBeEnabled();
  await expect(page.getByRole('alert')).toHaveCount(0);
  await goal.click();
  await page.getByRole('button', { name: /Continue/ }).click();
  await page.getByRole('button', { name: /Start from scratch/ }).click();
  await page.getByRole('button', { name: /Let’s begin/ }).click();
  await expect(page).toHaveURL(/\/learn\/session\//);
  await expect(page.getByRole('button', { name: 'Check', exact: true })).toBeVisible();
});
