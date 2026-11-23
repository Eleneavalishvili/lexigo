import { execFileSync } from 'node:child_process';
import { expect, test } from '@playwright/test';
import { registerLearner } from './test-account';
import { fundTestCard } from './simulated-premium';

const inboxPort = process.env['LEXIGO_E2E_INBOX_PORT'];
const database = process.env['LEXIGO_E2E_DATABASE'];
const spanish = '10000000-0000-0000-0000-000000000001';

test('a learner can open free practice after losing their last heart', async ({
  page,
  request,
  baseURL,
}) => {
  test.skip(!inboxPort || !database, 'Run with the disposable database and local inbox.');
  test.setTimeout(90_000);
  expect(database).toMatch(/^lexigo-browser-db-[a-f0-9]{32}$/);
  const { email } = await registerLearner(page, request, baseURL!, inboxPort!);
  expect(email).toMatch(/^learner-[a-f0-9-]+@example\.test$/);
  execFileSync(
    'docker',
    [
      'exec',
      '-i',
      database!,
      'psql',
      '-Xq',
      '-v',
      'ON_ERROR_STOP=1',
      '-U',
      'postgres',
      '-d',
      'postgres',
    ],
    {
      input: `INSERT INTO "LearnerHearts" ("UserId", "Count")
      SELECT "Id", 1 FROM "Users" WHERE "Email" = '${email}'
      ON CONFLICT ("UserId") DO UPDATE SET "Count" = 1, "RefillAt" = NULL;`,
    },
  );

  await page.goto(`/learn/${spanish}`);
  await page.getByRole('link', { name: /Choose my goal and starting point/ }).click();
  await page.getByRole('button', { name: /20 XP \/ day/ }).click();
  await page.getByRole('button', { name: /Continue/ }).click();
  await page.getByRole('button', { name: /Start from scratch/ }).click();
  await page.getByRole('button', { name: /Let’s begin/ }).click();
  await expect(page).toHaveURL(/\/learn\/session\//);
  await page.getByRole('radio', { name: / leche$/ }).press('Space');
  await page.getByRole('button', { name: 'Check', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Incorrect' })).toBeVisible();
  await page.getByRole('button', { name: /Continue/ }).click();
  await expect(page.getByRole('timer', { name: 'Time until all five hearts return' })).toHaveText(
    /^\s*(00:59:\d\d|01:00:00)\s*$/,
  );
  await page.getByRole('button', { name: /Finish & see results/ }).click();
  await expect(
    page.locator('app-lesson-result-stats').getByText('+0', { exact: true }),
  ).toBeVisible();
  await page.getByRole('button', { name: /Free practice · no hearts needed/ }).click();
  await expect(page.getByText(/Practice without losing hearts/)).toBeVisible();
  await page.getByRole('radio', { name: / leche$/ }).press('Space');
  await page.getByRole('button', { name: 'Check', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Incorrect' })).toBeVisible();
  await page.getByRole('button', { name: /Continue/ }).click();
  await expect(page.getByRole('heading', { name: 'Write this in English' })).toBeVisible();
  const practice = page.url();
  execFileSync(
    'docker',
    [
      'exec',
      '-i',
      database!,
      'psql',
      '-Xq',
      '-v',
      'ON_ERROR_STOP=1',
      '-U',
      'postgres',
      '-d',
      'postgres',
    ],
    {
      input: `UPDATE "LearnerHearts" SET "RefillAt" = now() - interval '1 second'
      WHERE "Count" = 0 AND "UserId" = (SELECT "Id" FROM "Users" WHERE "Email" = '${email}');`,
    },
  );
  await page.goto('/billing/hearts');
  await expect(page.locator('.current-hearts')).toContainText('5 / 5');
  await expect(page.getByRole('button', { name: /Confirm test purchase/ })).toBeDisabled();
  await page.goto(practice);
  await expect(page.getByRole('progressbar', { name: '1 of 12 answers saved' })).toBeVisible();
});

test('a five-GEL simulated refill resumes the same unfinished lesson', async ({
  page,
  request,
  baseURL,
}) => {
  test.skip(!inboxPort, 'Run with the disposable database and local inbox.');
  test.setTimeout(100_000);
  await registerLearner(page, request, baseURL!, inboxPort!);
  await fundTestCard(page);
  await page.goto(`/learn/${spanish}`);
  await page.getByRole('link', { name: /Choose my goal and starting point/ }).click();
  await page.getByRole('button', { name: /20 XP \/ day/ }).click();
  await page.getByRole('button', { name: /Continue/ }).click();
  await page.getByRole('button', { name: /Start from scratch/ }).click();
  await page.getByRole('button', { name: /Let’s begin/ }).click();
  await expect(page).toHaveURL(/\/learn\/session\//);
  const lesson = page.url();
  await page.getByRole('radio', { name: / leche$/ }).press('Space');
  await page.getByRole('button', { name: 'Check', exact: true }).click();
  await page.getByRole('button', { name: /Continue/ }).click();
  await page.getByRole('link', { name: /Explore a simulated heart refill/ }).click();
  const card = page.getByLabel(/Test card · GEL only/);
  await expect(card.locator('option').filter({ hasText: 'Study test card' })).toContainText(
    '20.00',
  );
  await card.selectOption({ index: 1 });
  await page.getByRole('button', { name: /Confirm test purchase/ }).click();
  const result = page.getByRole('status', { name: 'Your refill is saved.' });
  await expect(page.getByRole('heading', { name: 'Your refill is saved.' })).toBeVisible();
  await expect(page.getByText(/Charged:.*5\.00.*simulated funds/)).toBeVisible();
  await expect(page.locator('.current-hearts')).toContainText('5 / 5');
  await expect(card.locator('option').filter({ hasText: 'Study test card' })).toContainText(
    '15.00',
  );
  await expect(page.getByRole('button', { name: /Confirm test purchase/ })).toBeDisabled();
  await result.getByRole('link', { name: /Return to your saved learning/ }).click();
  await expect(page).toHaveURL(lesson);
  await expect(page.getByRole('progressbar', { name: '1 of 12 answers saved' })).toBeVisible();
});
